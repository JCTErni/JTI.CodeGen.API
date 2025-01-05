using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.Models.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeTriggerFunction
    {
        private readonly ILogger<CodeTriggerFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly Container _codeStatusContainer;
        private readonly string _databaseName;

        public CodeTriggerFunction(ILogger<CodeTriggerFunction> logger, CosmosDbService cosmosDbService, IConfiguration configuration)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _codeStatusContainer = _cosmosDbService.GetContainer(ConfigurationConstants.CodeStatusContainer);
            _databaseName = configuration[ConfigurationConstants.CosmosDbDatabaseName];
        }

        [Function("CodeStatusTriggerFunction")]
        public async Task CodeStatusTriggerFunction([CosmosDBTrigger(
            databaseName: "%CosmosDbDatabaseName%",
            containerName: "%CodesContainerName%",
            Connection = "CosmosDbConnectionString",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)] IReadOnlyList<Code> input)
        {
            if (input != null && input.Count > 0)
            {
                foreach (var codeItem in input)
                {
                    // Check if the item status is "Pending"
                    if (codeItem.Status == "Pending")
                    {
                        try
                        {
                            // Call the stored procedure
                            var response = await _codeStatusContainer.Scripts.ExecuteStoredProcedureAsync<CodeStatus>(
                                "createCodeStatusItem",
                                new PartitionKey(codeItem.EncryptedCode),
                                new dynamic[] { codeItem });

                            _logger.LogInformation($"[CodeStatusTriggerFunction] Created status item for code item with id: {codeItem.id}");
                        }
                        catch (CosmosException ex)
                        {
                            _logger.LogError($"[CodeStatusTriggerFunction] Error creating status item for code item with id: {codeItem.id}. Exception: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
