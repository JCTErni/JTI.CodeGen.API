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

            var codeContainerId = input.CodeContainerId;
            var codeByBatchContainerId = input.CodeByBatchContainerId;
            var codeOriginalMaxThroughput = input.CodeOriginalMaxThroughput;
            var codeByBatchOriginalMaxThroughput = input.CodeByBatchOriginalMaxThroughput;
            var numberOfCodes = input.NumberOfCodes;
            var codeLength = input.CodeLength;
            var batchSize = input.BatchSize;
            var totalBatches = input.TotalBatches;
            var batchNumber = input.Batch;
            var sequence = input.Sequence;

            var requestUnitPerItem = CodeGenerationConstants.requestUnitPerItem;
            var maxThroughputAdjustment = CodeGenerationConstants.maxThroughputAdjustment;

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[ParentInsertCodeOrchestrator] Adjusting throughput...");

            // Adjust throughput at the start of the orchestration
            await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = codeContainerId, Throughput = maxThroughputAdjustment });
            await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = codeByBatchContainerId, Throughput = maxThroughputAdjustment });

            try
            {
                var tasks = new List<Task>();

                for (int i = 0; i < totalBatches; i++)
                {
                    // Adjust the batch size for the last batch
                    int currentBatchSize = (i == totalBatches - 1) ? numberOfCodes - (i * batchSize) : batchSize;

                    var subOrchestratorInput = new InsertCodesOrchestratorInput
                    {
                        CodeLength = codeLength,
                        Batch = batchNumber,
                        Sequence = sequence,
                        BatchIndex = i,
                        BatchSize = currentBatchSize
                    };

                    // Start the sub-orchestrator
                    tasks.Add(context.CallSubOrchestratorAsync(nameof(InsertCodeOrchestrator), subOrchestratorInput));

                    // Introduce a delay between each sub-orchestrator call
                    var nextDelay = context.CurrentUtcDateTime.AddSeconds((requestUnitPerItem * batchSize) / maxThroughputAdjustment);
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
                await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = codeContainerId, Throughput = codeOriginalMaxThroughput });

                // Introduce a 1-minute delay
                var delayUntil = context.CurrentUtcDateTime.AddMinutes(1);
                await context.CreateTimer(delayUntil, CancellationToken.None);

                await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = codeByBatchContainerId, Throughput = codeByBatchOriginalMaxThroughput });
            }
        }

        [Function("InsertCodeOrchestrator")]
        public async Task InsertCodeOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var input = context.GetInput<InsertCodesOrchestratorInput>();

            var codeLength = input.CodeLength;
            var batchNumber = input.Batch;
            var sequence = input.Sequence;
            var batchIndex = input.BatchIndex;
            var batchSize = input.BatchSize;

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[SubOrchestrator] Executing InsertCodeActivity...");

            try
            {
                // Generate codes for the current batch
                var batchRequest = new GenerateCodeRequest
                {
                    NumberOfCodes = batchSize,
                    CodeLength = codeLength,
                    Batch = batchNumber,
                    Sequence = sequence
                };
                var codeBatch = _codeService.GenerateCodesAsync(batchRequest);

                var insertedCodes = await context.CallActivityAsync<List<Code>>(nameof(InsertCodeActivity), codeBatch);
                int insertedCount = insertedCodes.Count;
                if (insertedCount < codeBatch.Count)
                {
                    int remainingItemsCount = codeBatch.Count - insertedCount;
                    var request = new GenerateCodeRequest
                    {
                        NumberOfCodes = remainingItemsCount,
                        CodeLength = codeLength,
                        Batch = batchNumber,
                        Sequence = sequence,
                    };
                    var remainingItems = _codeService.GenerateCodesAsync(request);

                    // Retry insertion for remaining items
                    logger.LogInformation("[InsertCodeOrchestrator] Retrying insertion for {RemainingItemsCount} remaining codes.", remainingItemsCount);
                    insertedCodes = await context.CallActivityAsync<List<Code>>(nameof(InsertCodeActivity), remainingItems);
                }
            }
            catch (BatchInsertException ex)
            {
                logger.LogError(ex, "[InsertCodeOrchestrator] Batch insert failed. Generating new batch for remaining items. Exception: {ExceptionMessage}", ex.Message);

                // Calculate remaining items and generate new batch
                int remainingItemsCount = batchSize - ex.InsertedCount;
                var request = new GenerateCodeRequest
                {
                    NumberOfCodes = remainingItemsCount,
                    CodeLength = codeLength,
                    Batch = batchNumber,
                    Sequence = sequence,
                };
                var remainingItems = _codeService.GenerateCodesAsync(request);

                // Retry insertion for remaining items
                logger.LogInformation("[InsertCodeOrchestrator] Retrying insertion for {RemainingItemsCount} remaining codes after exception.", remainingItemsCount);
                var insertedCodes = await context.CallActivityAsync<List<Code>>(nameof(InsertCodeActivity), remainingItems);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[InsertCodeOrchestrator] Unexpected error occurred during batch insert. Exception: {ExceptionMesssage}", ex.Message);
                // Handle unexpected errors
                throw;
            }
        }

        [Function("InsertCodeActivity")]
        public async Task<List<Code>> InsertCodeActivity([ActivityTrigger] List<Code> codes)
        {
            var insertedCodes = new List<Code>();

            try
            {
                insertedCodes = await _cosmosDbService.BulkInsertAsync(codes, ConfigurationConstants.CodeContainer);
                int insertedCount = insertedCodes.Count;
                _logger.LogInformation("[InsertCodeActivity] Bulk insert completed successfully. Inserted {InsertedCount} items.", insertedCount);
                return insertedCodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InsertCodeActivity] Bulk insert failed. Inserted {InsertedCount} items before failure. Exception: {ExceptionMessage}", insertedCodes.Count, ex.Message);
                throw new BatchInsertException("Bulk insert failed.", insertedCodes.Count, ex);
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
                    _logger.LogInformation($"[AdjustThroughputActivity] {input.ContainerId} Throughput adjusted to {input.Throughput.Value} RU/s.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AdjustThroughputActivity] An error occurred while adjusting throughput of {input.ContainerId}. Exception: {ex.Message}");
                throw; // Rethrow the exception to propagate it back to the orchestrator
            }
        }

        [Function("CodeTriggerAdjustThroughputActivity")]
        public async Task CodeTriggerAdjustThroughputActivity([ActivityTrigger] AdjustThroughputInput input)
        {
            try
            {
                var container = _cosmosDbService.GetContainer(input.ContainerId);

                if (input.Throughput.HasValue)
                {
                    var throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(input.Throughput.Value);
                    await container.ReplaceThroughputAsync(throughputProperties);
                    _logger.LogInformation("[CodeTriggerAdjustThroughputActivity] Throughput adjusted to {Throughput} RU/s.", input.Throughput.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CodeTriggerAdjustThroughputActivity] An error occurred while adjusting throughput. Exception: {ExceptionMessage}", ex.Message);
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
