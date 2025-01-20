using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeOrchestratorFunction
    {
        private readonly ILogger<CodeOrchestratorFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        public CodeOrchestratorFunction(ILogger<CodeOrchestratorFunction> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("InsertCodeOrchestrator")]
        public static async Task InsertCodeOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var input = context.GetInput<InsertCodesOrchestratorInput>();

            var containerId = input.ContainerId;
            var originalMaxThroughput = input.OriginalMaxThroughput;
            var codeBatches = input.CodeBatches;

            var retryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(5),
                backoffCoefficient: 2.0));

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[InsertCodeOrchestrator] Executing InsertCodeActivity...");

            // Adjust throughput at the start of the orchestration
            await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = containerId, Throughput = 10000 }, retryOptions);

            try
            {
                var batchInsertTasks = new List<Task>();

                foreach (var batch in codeBatches)
                {
                    batchInsertTasks.Add(context.CallActivityAsync(nameof(InsertCodeActivity), batch, retryOptions));
                }

                await Task.WhenAll(batchInsertTasks);
            }
            finally
            {
                logger.LogInformation("[InsertCodeOrchestrator] Reverting throughput...");
                // Revert throughput at the end of the orchestration
                await context.CallActivityAsync(nameof(AdjustThroughputActivity), new AdjustThroughputInput { ContainerId = containerId, Throughput = originalMaxThroughput }, retryOptions);
            }
        }

        [Function("InsertCodeActivity")]
        public async Task InsertCodeActivity([ActivityTrigger] List<Code> codes)
        {
            try
            {
                await _cosmosDbService.BulkInsertAsync(codes, ConfigurationConstants.CodeContainer);
                _logger.LogInformation("[InsertCodeActivity] Bulk insert completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InsertCodeActivity] Bulk insert failed.");
                throw;
            }
        }

        [Function("AdjustThroughputActivity")]
        public async Task AdjustThroughputActivity([ActivityTrigger] AdjustThroughputInput input)
        {
            var container = _cosmosDbService.GetContainer(input.ContainerId);

            if (input.Throughput.HasValue)
            {
                var throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(input.Throughput.Value);
                await container.ReplaceThroughputAsync(throughputProperties);
            }
        }

        public class InsertCodesOrchestratorInput
        {
            public string ContainerId { get; set; }
            public int? OriginalMaxThroughput { get; set; }
            public List<List<Code>> CodeBatches { get; set; }
        }

        public class AdjustThroughputInput
        {
            public string ContainerId { get; set; }
            public int? Throughput { get; set; }
        }

    }
}
