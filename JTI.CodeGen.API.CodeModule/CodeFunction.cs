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
        public async Task<HttpResponseData> GenerateCode([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context, [DurableClient] DurableTaskClient durableClient)
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

            // Start the orchestration
            string instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(CodeOrchestratorFunction.InsertCodeOrchestrator), orchestratorInput);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Codes are being generated and inserted.");

            _logger.LogInformation("[Generate Codes] Function Completed.");

            return response;
        }
    }
}
