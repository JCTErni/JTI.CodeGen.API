using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Cosmos;
using System.Collections.Concurrent;
using System.Net;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.Constants;
using System.Reflection;
using JTI.CodeGen.API.CodeModule.Dtos.Exceptions;

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

        public async Task<List<T>> BulkInsertAsync<T>(IEnumerable<T> items, string containerName) 
        {
            var container = GetContainer(containerName);

            PropertyInfo partitionKeyProperty = null;
            if (container.Id == ConfigurationConstants.CodeContainer)
            {
                partitionKeyProperty = typeof(T).GetProperty("code");
            }else if (container.Id == ConfigurationConstants.CodeByBatchContainer)
            {
                partitionKeyProperty = typeof(T).GetProperty("batch");
            }

            if (partitionKeyProperty == null)
            {
                throw new ArgumentException($"The type {typeof(T).Name} does not contain the needed property.");
            }

            var insertedCodes = new ConcurrentBag<T>();

            try
            {
                List<Task> concurrentTasks = new List<Task>();
                foreach (var item in items)
                {
                    var partitionKeyValue = partitionKeyProperty.GetValue(item);
                    concurrentTasks.Add(InsertWithRetryAsync(container, item, partitionKeyValue, partitionKeyProperty.Name, () => insertedCodes.Add(item)));
                }
                await Task.WhenAll(concurrentTasks);
            }
            catch (Exception ex)
            {
                // Optionally, you can rethrow the exception or throw a new one with additional context
                if(container.Id == ConfigurationConstants.CodeByBatchContainer)
                {
                    throw new BulkInsertException<T>($"An error occurred during bulk insert. See inner exception for details. Inserted: {insertedCodes.Count} Exception: {ex.Message}", insertedCodes.ToList(), ex);
                }
                throw new Exception($"An error occurred during bulk insert. See inner exception for details. Inserted: {insertedCodes.Count} Exception: {ex.Message}", ex);
            }

            return insertedCodes.ToList();
        }

        private async Task InsertWithRetryAsync<T>(Container container, T item, object partitionKeyValue, string partitionKeyName, Action onSuccess)
        {
            int maxRetries = CodeGenerationConstants.maxInsertRetryTime;
            int retryCount = 0;
            bool inserted = false;

            while (!inserted && retryCount < maxRetries)
            {
                try
                {
                    await container.CreateItemAsync(item, new PartitionKey(partitionKeyValue.ToString()));
                    inserted = true;
                    onSuccess();
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // Handle conflict by generating a new code and retrying
                    if (item is Code codeItem && partitionKeyName == "code")
                    {
                        var itemLength = codeItem.code.Length;
                        codeItem.UpdateCode(CodeServiceHelper.GenerateRandomCode(itemLength));
                        partitionKeyValue = codeItem.code;
                    }
                    retryCount++;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Handle throttling
                    await Task.Delay(ex.RetryAfter.GetValueOrDefault(TimeSpan.FromSeconds(CodeGenerationConstants.insertRetryTime)));
                    retryCount++;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to insert item after {retryCount} retries. See inner exception for details. Exception: {ex.Message}", ex);
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
