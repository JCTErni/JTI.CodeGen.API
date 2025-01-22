using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
using JTI.CodeGen.API.CodeModule.Dtos;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeOrchestratorFunction
    {
        private readonly ILogger<CodeOrchestratorFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly ICodeService _codeService;
        public CodeOrchestratorFunction(ILogger<CodeOrchestratorFunction> logger, CosmosDbService cosmosDbService, ICodeService codeService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _codeService = codeService;
        }

        [Function("ParentInsertCodeOrchestrator")]
        public async Task ParentInsertCodeOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var input = context.GetInput<ParentInsertCodeOrchestratorInput>();

            var containerId = input.ContainerId;
            var originalMaxThroughput = input.OriginalMaxThroughput;
            var codeBatches = input.CodeBatches;
            var batchNumber = input.Batch;
            var sequence = input.Sequence;

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[ParentInsertCodeOrchestrator] Adjusting throughput...");

            // Adjust throughput at the start of the orchestration
            await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = containerId, Throughput = 10000 });

            try
            {
                var tasks = new List<Task>();

                foreach (var batch in codeBatches)
                {
                    var subOrchestratorInput = new InsertCodesOrchestratorInput
                    {
                        ContainerId = containerId,
                        OriginalMaxThroughput = originalMaxThroughput,
                        CodeBatch = batch,
                        Batch = batchNumber,
                        Sequence = sequence
                    };

                    tasks.Add(context.CallSubOrchestratorAsync(nameof(InsertCodeOrchestrator), subOrchestratorInput));

                    // Introduce a 23 - second delay between each iteration
                    var nextDelay = context.CurrentUtcDateTime.AddSeconds(23);
                    await context.CreateTimer(nextDelay, CancellationToken.None);
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ParentInsertCodeOrchestrator] An error occurred during orchestration. Exception: {ExceptionMessage}", ex.Message);
                throw; // Propagate the exception
            }
            finally
            {
                logger.LogInformation("[ParentOrchestrator] Reverting throughput...");
                // Revert throughput at the end of the orchestration
                await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = containerId, Throughput = originalMaxThroughput });
            }
        }

        [Function("InsertCodeOrchestrator")]
        public async Task InsertCodeOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var input = context.GetInput<InsertCodesOrchestratorInput>();

            var codeBatch = input.CodeBatch;
            var batchNumber = input.Batch;
            var sequence = input.Sequence;

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[SubOrchestrator] Executing InsertCodeActivity...");

            try
            {
                int insertedCount = await context.CallActivityAsync<int>(nameof(InsertCodeActivity), codeBatch);
                if (insertedCount < codeBatch.Count)
                {
                    int remainingItemsCount = codeBatch.Count - insertedCount;
                    var request = new GenerateCodeRequest
                    {
                        NumberOfCodes = remainingItemsCount,
                        Batch = batchNumber,
                        Sequence = sequence,
                    };
                    var remainingItems = _codeService.GenerateCodesAsync(request);

                    // Retry insertion for remaining items
                    logger.LogInformation("[InsertCodeOrchestrator] Retrying insertion for {RemainingItemsCount} remaining codes.", remainingItemsCount);
                    await context.CallActivityAsync(nameof(InsertCodeActivity), remainingItems);
                }
            }
            catch (BatchInsertException ex)
            {
                logger.LogError(ex, "[InsertCodeOrchestrator] Batch insert failed. Generating new batch for remaining items. Exception: {ExceptionMessage}", ex.Message);

                // Calculate remaining items and generate new batch
                int remainingItemsCount = codeBatch.Count - ex.InsertedCount;
                var request = new GenerateCodeRequest
                {
                    NumberOfCodes = remainingItemsCount,
                    Batch = batchNumber,
                    Sequence = sequence,
                };
                var remainingItems = _codeService.GenerateCodesAsync(request);

                // Retry insertion for remaining items
                logger.LogInformation("[InsertCodeOrchestrator] Retrying insertion for {RemainingItemsCount} remaining codes after exception.", remainingItemsCount);
                await context.CallActivityAsync(nameof(InsertCodeActivity), remainingItems);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[InsertCodeOrchestrator] Unexpected error occurred during batch insert. Exception: {ExceptionMesssage}", ex.Message);
                // Handle unexpected errors
                throw;
            }
        }

        [Function("InsertCodeActivity")]
        public async Task<int> InsertCodeActivity([ActivityTrigger] List<Code> codes)
        {
            int insertedCount = 0;

            try
            {
                insertedCount = await _cosmosDbService.BulkInsertAsync(codes, ConfigurationConstants.CodeContainer);
                _logger.LogInformation("[InsertCodeActivity] Bulk insert completed successfully. Inserted {InsertedCount} items.", insertedCount);
                return insertedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InsertCodeActivity] Bulk insert failed. Inserted {InsertedCount} items before failure. Exception: {ExceptionMessage}", insertedCount, ex.Message);
                throw new BatchInsertException("Bulk insert failed.", insertedCount, ex);
            }
        }

        [Function("AdjustThroughputActivity")]
        public async Task AdjustThroughputActivity([ActivityTrigger] AdjustThroughputInput input)
        {
            try
            {
                var container = _cosmosDbService.GetContainer(input.ContainerId);

                if (input.Throughput.HasValue)
                {
                    var throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(input.Throughput.Value);
                    await container.ReplaceThroughputAsync(throughputProperties);
                    _logger.LogInformation("[AdjustThroughputActivity] Throughput adjusted to {Throughput} RU/s.", input.Throughput.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AdjustThroughputActivity] An error occurred while adjusting throughput. Exception: {ExceptionMessage}", ex.Message);
                throw; // Rethrow the exception to propagate it back to the orchestrator
            }
        }

        public class BatchInsertException : Exception
        {
            public int InsertedCount { get; }

            public BatchInsertException(string message, int insertedCount, Exception innerException)
                : base(message, innerException)
            {
                InsertedCount = insertedCount;
            }
        }
    }
}
