using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using Newtonsoft.Json;
using static JTI.CodeGen.API.CodeModule.CodeOrchestratorFunction;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;

namespace JTI.CodeGen.API.CodeModule
{
    public class TestHelperFunction
    {
        private readonly ILogger<TestHelperFunction> _logger;
        private readonly Container _codeContainer;
        private readonly CosmosDbService _cosmosDbService;
        private readonly ICodeService _codeService;

        public TestHelperFunction(ILogger<TestHelperFunction> logger, CosmosDbService cosmosDbService, ICodeService codeService)
        {
            _logger = logger;
            _codeContainer = cosmosDbService.GetContainer(ConfigurationConstants.CodeContainer);
            _cosmosDbService = cosmosDbService;
            _codeService = codeService;
        }

        [Function("delete-all-except")]
        public async Task<HttpResponseData> DeleteAllExcept([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Delete All Except] Function Started.");

            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string id = queryParams["id"];
            string code = queryParams["code"];

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("[Delete All Except] Missing required parameters.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required parameters.");
                return badRequestResponse;
            }

            _logger.LogInformation("[Delete All Except] Deleting all entries except Id: {Id}, Code: {Code}", id, code);

            // Query for entries to delete
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id != @id AND c.EncryptedCode != @code")
                .WithParameter("@id", id)
                .WithParameter("@code", code);

            var tasks = new List<Task>();
            using (var iterator = _codeContainer.GetItemQueryIterator<Code>(query))
            {
                while (iterator.HasMoreResults)
                {
                    var queryResponse = await iterator.ReadNextAsync();
                    foreach (var item in queryResponse)
                    {
                        tasks.Add(DeleteItemAsync(item));
                    }
                }
            }

            await Task.WhenAll(tasks);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("All entries except the specified one have been deleted.");

            _logger.LogInformation("[Delete All Except] Function Completed.");

            return response;
        }

        private async Task DeleteItemAsync(Code item)
        {
            int maxRetryAttempts = 5;
            int delayMilliseconds = 1000;

            for (int attempt = 0; attempt < maxRetryAttempts; attempt++)
            {
                try
                {
                    await _codeContainer.DeleteItemAsync<Code>(item.id, new PartitionKey(item.code));
                    _logger.LogInformation("Deleted item with Id: {Id}", item.id);
                    return; // Exit the method if the deletion is successful
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning(ex, "Rate limiting encountered while deleting item with Id: {Id}. Retrying...", item.id);
                    await Task.Delay(delayMilliseconds * (int)Math.Pow(2, attempt)); // Exponential backoff
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete item with Id: {Id}", item.id);
                    throw; // Rethrow the exception to be handled by the caller
                }
            }

            throw new Exception($"Failed to delete item with Id: {item.id} after {maxRetryAttempts} attempts.");
        }

        [Function("count-items")]
        public async Task<HttpResponseData> CountItems([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Count Items] Function Started.");

            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");

            int itemCount = 0;
            using (var iterator = _codeContainer.GetItemQueryIterator<int>(query))
            {
                while (iterator.HasMoreResults)
                {
                    var queryResponse = await iterator.ReadNextAsync();
                    itemCount += queryResponse.FirstOrDefault();
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Total number of items: {itemCount}");

            _logger.LogInformation("[Count Items] Function Completed.");

            return response;
        }

        [Function("insert-duplicate")]
        public async Task<HttpResponseData> GenerateCode([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context, [DurableClient] DurableTaskClient durableClient)
        {
            var startTime = DateTime.UtcNow;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var code = JsonConvert.DeserializeObject<Code>(requestBody);
            var dupeCode = new Code
            {
                id = code.id,
                code = code.code,
                batch = code.batch,
                sequence = code.sequence,
                lastupdate = code.lastupdate,
                status = code.status
            };

            // Adjust autoscale max throughput to 10000 RU/s before bulk insert
            var container = _cosmosDbService.GetContainer(ConfigurationConstants.CodeContainer);
            ThroughputProperties throughputResponse = await container.ReadThroughputAsync(requestOptions: null);
            int? originalMaxThroughput = throughputResponse.AutoscaleMaxThroughput;

            List<Code> codes = new List<Code> { code };
            code.id = Guid.NewGuid().ToString();
            dupeCode.id = Guid.NewGuid().ToString();
            _logger.LogInformation($"[Generate Codes] Original Code1: {code.code}.");
            _logger.LogInformation($"[Generate Codes] Original Code2: {dupeCode.code}.");

            // Create batches of 10,000 items each
            int batchSize = 10000;
            var codeBatches = codes
                .Select((code, index) => new { code, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.code).ToList())
                .ToList();

            var orchestratorInput = new InsertCodesOrchestratorInput
            {
                ContainerId = ConfigurationConstants.CodeContainer,
                OriginalMaxThroughput = originalMaxThroughput,
                CodeBatches = codeBatches
            };

            // Start the orchestration
            string instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(CodeOrchestratorFunction.InsertCodeOrchestrator), orchestratorInput);

            var response = req.CreateResponse(HttpStatusCode.OK);

            var endTime = DateTime.UtcNow;
            _logger.LogInformation("[Generate Codes] Function Completed at {EndTime}. Duration: {Duration} seconds.", endTime, (endTime - startTime).TotalSeconds);

            _logger.LogInformation($"[Generate Codes] Code1: {code.code}.");
            _logger.LogInformation($"[Generate Codes] Code2: {dupeCode.code}.");
            return response;
        }

        [Function("general")]
        public async Task<HttpResponseData> General([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context, [DurableClient] DurableTaskClient durableClient)
        {
            _logger.LogInformation("[Generate Codes] Function Started.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var generateCodeRequest = JsonConvert.DeserializeObject<GenerateCodeRequest>(requestBody);

            if (generateCodeRequest == null || generateCodeRequest.NumberOfCodes <= 0 || string.IsNullOrEmpty(generateCodeRequest.Brand))
            {
                _logger.LogWarning("[Generate Codes] Missing required parameters.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required parameters.");
                return badRequestResponse;
            }

            _logger.LogInformation("[Generate Codes] Generating codes for Brand: {Brand}, Number of Codes: {NumberOfCodes}", generateCodeRequest.Brand, generateCodeRequest.NumberOfCodes);

            var codes = _codeService.GenerateCodesAsync(generateCodeRequest);

            // Adjust autoscale max throughput to 10000 RU/s before bulk insert
            var container = _cosmosDbService.GetContainer(ConfigurationConstants.CodeContainer);

            ThroughputProperties throughputResponse = await container.ReadThroughputAsync(requestOptions: null);
            int? originalMaxThroughput = throughputResponse.AutoscaleMaxThroughput;

            // Create batches of 10,000 items each
            int batchSize = 10000;
            var codeBatches = codes
                .Select((code, index) => new { code, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.code).ToList())
                .ToList();

            var orchestratorInput = new InsertCodesOrchestratorInput
            {
                ContainerId = ConfigurationConstants.CodeContainer,
                OriginalMaxThroughput = originalMaxThroughput,
                CodeBatches = codeBatches
            };

            var adjustThroughputInput = new AdjustThroughputInput { ContainerId = orchestratorInput.ContainerId, Throughput = 10000 };

            if (adjustThroughputInput.Throughput.HasValue)
            {
                var throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(adjustThroughputInput.Throughput.Value);
                await container.ReplaceThroughputAsync(throughputProperties);
            }

            foreach (var batch in codeBatches)
            {
                var partitionKeyProperty = typeof(Code).GetProperty("code");
                List<Task> concurrentTasks = new();
                foreach (var item in batch)
                {
                    var partitionKeyValue = partitionKeyProperty.GetValue(item);
                    concurrentTasks.Add(container.CreateItemAsync(item, new PartitionKey(Convert.ToString(partitionKeyValue))));
                }
                await Task.WhenAll(concurrentTasks);
            }

            //var partitionKeyProperty = typeof(Code).GetProperty("code");
            //List<Task> concurrentTasks = new();
            //foreach (var item in codes)
            //{
            //    item.code = "9PHXZPIF9";
            //    var partitionKeyValue = partitionKeyProperty.GetValue(item);
            //    await container.CreateItemAsync(item, new PartitionKey(Convert.ToString(partitionKeyValue)));
            //}

            // Create a bogus response

            var throughputProperties2 = ThroughputProperties.CreateAutoscaleThroughput((int)originalMaxThroughput);
            await container.ReplaceThroughputAsync(throughputProperties2);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("This is a bogus response.");

            return response;
        }

        private async Task InsertWithRetryAsync<T>(Container container, T item, object partitionKeyValue)
        {
            int maxRetries = 3;
            int retryCount = 0;
            bool inserted = false;

            while (!inserted && retryCount < maxRetries)
            {
                try
                {
                    await container.CreateItemAsync(item, new PartitionKey(Convert.ToString(partitionKeyValue)));
                    inserted = true;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // Handle conflict by generating a new code and retrying
                    if (item is Code codeItem)
                    {
                        codeItem.UpdateCode(CodeServiceHelper.GenerateRandomCode(9));
                        partitionKeyValue = codeItem.code;
                    }
                    retryCount++;
                }
            }

            if (!inserted)
            {
                throw new Exception("Failed to insert item after multiple retries due to code conflicts.");
            }
        }

        public class AdjustThroughputInput
        {
            public string ContainerId { get; set; }
            public int? Throughput { get; set; }
        }
    }
}
