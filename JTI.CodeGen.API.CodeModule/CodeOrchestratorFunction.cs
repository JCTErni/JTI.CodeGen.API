using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.Models.Entities;
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
            var codes = context.GetInput<List<Code>>();

            // Define your smaller batch size here
            var smallerBatchSize = 100; // Smaller batch size for further division
            var smallerBatches = new List<List<Code>>();

            var retryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(5),
                backoffCoefficient: 2.0));

            var logger = context.CreateReplaySafeLogger<CodeOrchestratorFunction>();
            logger.LogInformation("[InsertCodeOrchestrator] Executing InsertCodeActivity...");

            for (int i = 0; i < codes.Count; i += smallerBatchSize)
            {
                smallerBatches.Add(codes.GetRange(i, Math.Min(smallerBatchSize, codes.Count - i)));
            }

            var tasks = new List<Task>();

            foreach (var batch in smallerBatches)
            {
                tasks.Add(context.CallActivityAsync(nameof(InsertCodeActivity), batch, retryOptions));
            }

            await Task.WhenAll(tasks);
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
    }
}
