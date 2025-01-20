using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Cosmos;
using System.Collections.Concurrent;
using System.Net;
using JTI.CodeGen.API.CodeModule.Helpers;

namespace JTI.CodeGen.API.CodeModule.DataAccess
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;

        public CosmosDbService(string connectionString, string databaseName)
        {
            var options = new CosmosClientOptions
            {
                AllowBulkExecution = true // Enable bulk execution
            };

            _cosmosClient = new CosmosClient(connectionString, options);
            _database = _cosmosClient.GetDatabase(databaseName);
        }

        public Container GetContainer(string containerName)
        {
            var container = _database.GetContainer(containerName);
            return container;
        }

        public async Task<ItemResponse<T>> AddItemAsync<T>(T item, string containerName, PartitionKey partitionKey)
        {
            var container = GetContainer(containerName);
            return await container.CreateItemAsync(item, partitionKey);
        }

        public async Task BulkInsertAsync<T>(IEnumerable<T> items, string containerName) 
        {
            var container = GetContainer(containerName);
            var partitionKeyProperty = typeof(T).GetProperty("code");

            if (partitionKeyProperty == null)
            {
                throw new ArgumentException($"The type {typeof(T).Name} does not contain a property named 'code'.");
            }

            try
            {
                List<Task> concurrentTasks = new();
                foreach (var item in items)
                {
                    var partitionKeyValue = partitionKeyProperty.GetValue(item);
                    concurrentTasks.Add(InsertWithRetryAsync(container, item, partitionKeyValue));
                }
                await Task.WhenAll(concurrentTasks);
            }
            catch (Exception ex)
            {
                // Optionally, you can rethrow the exception or throw a new one with additional context
                throw new Exception("An error occurred during bulk insert. See inner exception for details.", ex);
            }
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

        // Add more methods for interacting with Cosmos DB as needed
    }
}
