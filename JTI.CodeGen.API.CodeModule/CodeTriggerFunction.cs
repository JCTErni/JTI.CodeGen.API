using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using JTI.CodeGen.API.CodeModule.Dtos;
using Microsoft.DurableTask;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using static JTI.CodeGen.API.CodeModule.CodeOrchestratorFunction;
using System.Data.Common;
using Microsoft.DurableTask.Client;
using JTI.CodeGen.API.CodeModule.Dtos.Exceptions;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeTriggerFunction
    {
        private readonly ILogger<CodeTriggerFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly Container _codeByBatchContainer;
        private readonly string _databaseName;
        private readonly ICodeService _codeService;

        public CodeTriggerFunction(ILogger<CodeTriggerFunction> logger, CosmosDbService cosmosDbService, IConfiguration configuration, ICodeService codeService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _codeByBatchContainer = _cosmosDbService.GetContainer(ConfigurationConstants.CodeByBatchContainer);
            _databaseName = configuration[ConfigurationConstants.CosmosDbDatabaseName];
            _codeService = codeService;
        }

        [Function("CodesByBatchTriggerFunction")]
        public async Task CodesByBatchTriggerFunction([CosmosDBTrigger(
            databaseName: "%CosmosDbDatabaseName%",
            containerName: "%CodesContainerName%",
            Connection = "CosmosDbConnectionString",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true,
            MaxItemsPerInvocation = 9000)] IReadOnlyList<Code> input,
            [DurableClient] DurableTaskClient durableClient)
        {
            if (input != null && input.Count > 0)
            {
                var batch = input[0].batch;

                // Adjust autoscale max throughput to 10000 RU/s before bulk insert
                var container = _cosmosDbService.GetContainer(ConfigurationConstants.CodeByBatchContainer);
                ThroughputProperties throughputResponse = await container.ReadThroughputAsync(requestOptions: null);
                int? originalMaxThroughput = throughputResponse.AutoscaleMaxThroughput;

                // Create the input for the orchestrator
                var orchestratorInput = new CodeTriggerOrchestratorInput
                {
                    ContainerId = ConfigurationConstants.CodeByBatchContainer,
                    OriginalMaxThroughput = originalMaxThroughput,
                    BatchSize = input.Count,
                    Codes = input.ToList()
                };

                // Start a new orchestration instance for each batch
                await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(CodeTriggerOrchestrator), orchestratorInput);
                _logger.LogInformation($"[CodesByBatchTriggerFunction] Started orchestration for batch {batch}.");
            }
        }

        [Function("CodeTriggerOrchestrator")]
        public async Task CodeTriggerOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var input = context.GetInput<CodeTriggerOrchestratorInput>();

            var containerId = input.ContainerId;
            var originalMaxThroughput = input.OriginalMaxThroughput;
            var batchSize = input.BatchSize;
            var codes = input.Codes;

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[CodeTriggerOrchestrator] Executing CodeTriggerInsertCodeActivity...");

            try
            {
                var insertedCodes = await context.CallActivityAsync<List<Code>>(nameof(CodeTriggerInsertCodeActivity), codes);
            }
            catch (CodeTriggerBatchInsertException ex)
            {
                logger.LogError(ex, "[CodeTriggerOrchestrator] Batch insert failed. Retrying to insert remaining items. Exception: {ExceptionMessage}", ex.Message);

                // Calculate remaining items and generate new batch
                var insertedCodes = ex.InsertedCodes;
                var remainingCodes = GetCodesToBeInserted(codes, insertedCodes);

                // Retry insertion for remaining items
                logger.LogInformation("[CodeTriggerOrchestrator] Retrying insertion for {RemainingItemsCount} remaining codes after exception.", remainingCodes.Count);
                insertedCodes = await context.CallActivityAsync<List<Code>>(nameof(CodeTriggerInsertCodeActivity), remainingCodes);
            }
        }

        [Function("CodeTriggerInsertCodeActivity")]
        public async Task<List<Code>> CodeTriggerInsertCodeActivity([ActivityTrigger] List<Code> codes)
        {
            var insertedCodes = new List<Code>();

            try
            {
                insertedCodes = await _cosmosDbService.BulkInsertAsync(codes, ConfigurationConstants.CodeByBatchContainer);
                _logger.LogInformation("[CodeTriggerInsertCodeActivity] Bulk insert completed successfully. Inserted {InsertedCount} items.", insertedCodes.Count);
                return insertedCodes;
            }
            catch (BulkInsertException<Code> bulkInsertException)
            {
                _logger.LogError(bulkInsertException, "[CodeTriggerInsertCodeActivity] Bulk insert failed. Inserted {InsertedCount} items before failure. Exception: {ExceptionMessage}", insertedCodes.Count, bulkInsertException.Message);
                throw new CodeTriggerBatchInsertException("Bulk insert failed.", insertedCodes, bulkInsertException);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CodeTriggerInsertCodeActivity] Bulk insert failed. Inserted {InsertedCount} items before failure. Exception: {ExceptionMessage}", insertedCodes.Count, ex.Message);
                throw new CodeTriggerBatchInsertException("Bulk insert failed.", insertedCodes, ex);
            }
        }

        public List<Code> GetCodesToBeInserted(List<Code> originalCodes, List<Code> insertedCodes)
        {
            // Assuming CodeValue is the unique identifier for the Code class
            var insertedCodeValues = new HashSet<string>(insertedCodes.Select(c => c.code));
            var codesToBeInserted = originalCodes.Where(c => !insertedCodeValues.Contains(c.code)).ToList();
            return codesToBeInserted;
        }
    }
}
