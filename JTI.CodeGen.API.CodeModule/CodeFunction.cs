using AutoMapper;
using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Constants;
using static JTI.CodeGen.API.CodeModule.CodeOrchestratorFunction;
using JTI.CodeGen.API.CodeModule.Entities;


namespace JTI.CodeGen.API.CodeModule
{
    public class CodeFunction
    {
        private readonly ILogger<CodeFunction> _logger;
        private readonly ICodeService _codeService;
        private readonly CosmosDbService _cosmosDbService;

        public CodeFunction(ILogger<CodeFunction> logger, ICodeService codeService, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _codeService = codeService;
            _cosmosDbService = cosmosDbService;
        }

        [Function("generate-codes")]
        public async Task<HttpResponseData> GenerateCode([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext context, [DurableClient] DurableTaskClient durableClient)
        {
            _logger.LogInformation("[Generate Codes] Function Started.");
            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var generateCodeRequest = JsonConvert.DeserializeObject<GenerateCodeRequest>(requestBody);

            var numberOfCodes = generateCodeRequest.NumberOfCodes;
            var brand = generateCodeRequest.Brand;
            var batch = generateCodeRequest.Batch;
            var sequence = generateCodeRequest.Sequence;

            if (generateCodeRequest == null || 
                numberOfCodes <= 0 || 
                string.IsNullOrEmpty(brand) || 
                string.IsNullOrEmpty(batch) || 
                string.IsNullOrEmpty(sequence))
            {
                _logger.LogWarning("[Generate Codes] Missing required parameters.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required parameters.");
                return badRequestResponse;
            }

            _logger.LogInformation("[Generate Codes] Generating codes for Brand: {Brand}, Number of Codes: {NumberOfCodes}", generateCodeRequest.Brand, generateCodeRequest.NumberOfCodes);

            // Adjust autoscale max throughput to 10000 RU/s before bulk insert
            var container = _cosmosDbService.GetContainer(ConfigurationConstants.CodeContainer);
            ThroughputProperties throughputResponse = await container.ReadThroughputAsync(requestOptions: null);
            int? originalMaxThroughput = throughputResponse.AutoscaleMaxThroughput;

            // Create batches of 10,000 items each
            int batchSize = 10000;
            var codeBatches = new List<List<Code>>();

            for (int i = 0; i < numberOfCodes; i += batchSize)
            {
                var batchRequest = new GenerateCodeRequest
                {
                    NumberOfCodes = Math.Min(batchSize, numberOfCodes - i),
                    Brand = brand,
                    Batch = batch,
                    Sequence = sequence
                };
                var batchCodes = _codeService.GenerateCodesAsync(batchRequest);
                codeBatches.Add(batchCodes);
            }

            var orchestratorInput = new ParentInsertCodeOrchestratorInput
            {
                ContainerId = ConfigurationConstants.CodeContainer,
                OriginalMaxThroughput = originalMaxThroughput,
                CodeBatches = codeBatches,
                Batch = batch,
                Sequence = sequence
            };

            try
            {
                // Start a new orchestration instance for each batch
                await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(CodeOrchestratorFunction.ParentInsertCodeOrchestrator), orchestratorInput);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Codes are being generated and inserted.");
                _logger.LogInformation("[Generate Codes] Function Completed.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Generate Codes] An error occurred while generating codes.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while generating codes.");
                return errorResponse;
            }
        }
    }
}
