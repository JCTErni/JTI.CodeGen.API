using Microsoft.Azure.Cosmos;
using System.Collections.Concurrent;

namespace JTI.CodeGen.API.Common.DataAccess
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly ConcurrentDictionary<string, Container> _containers;

        public CosmosDbService(string connectionString, string databaseName)
        {
            var options = new CosmosClientOptions
            {
                AllowBulkExecution = true // Enable bulk execution
            };

            _cosmosClient = new CosmosClient(connectionString, options);
            _database = _cosmosClient.GetDatabase(databaseName);
            _containers = new ConcurrentDictionary<string, Container>();

            InitializeContainersAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeContainersAsync()
        {
            var containerIterator = _database.GetContainerQueryIterator<ContainerProperties>();
            while (containerIterator.HasMoreResults)
            {
                foreach (var containerProperties in await containerIterator.ReadNextAsync())
                {
                    var container = _database.GetContainer(containerProperties.Id);
                    _containers.TryAdd(containerProperties.Id, container);
                }
            }
        }

        public Container GetContainer(string containerKey)
        {
            if (_containers.TryGetValue(containerKey, out var container))
            {
                return container;
            }
            throw new InvalidOperationException($"Container with key '{containerKey}' not found.");
        }

        public async Task<ItemResponse<T>> AddItemAsync<T>(T item, string containerName, PartitionKey partitionKey)
        {
            var container = GetContainer(containerName);
            return await container.CreateItemAsync(item, partitionKey);
        }

        public async Task BulkInsertAsync<T>(IEnumerable<T> items, string containerName) where T : class
        {
            var container = GetContainer(containerName);
            var partitionKeyProperty = typeof(T).GetProperty("EncryptedCode");

            if (partitionKeyProperty == null)
            {
                throw new InvalidOperationException("EncryptedCode property not found on type T.");
            }

            var partitionKeyValues = items
                .Select(item => partitionKeyProperty.GetValue(item)?.ToString())
                .Distinct()
                .ToList();

            foreach (var partitionKeyValue in partitionKeyValues)
            {
                var itemsForPartition = items
                    .Where(item => partitionKeyProperty.GetValue(item)?.ToString() == partitionKeyValue)
                    .ToList();

                var batch = container.CreateTransactionalBatch(new PartitionKey(partitionKeyValue));

                foreach (var item in itemsForPartition)
                {
                    batch.CreateItem(item);
                }

                var response = await batch.ExecuteAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Bulk insert failed with status code {response.StatusCode}");
                }
            }
        }

        private async Task CaptureOperationResponse<T>(Task<ItemResponse<T>> task, T item)
        {
            try
            {
                var response = await task;
                Console.WriteLine($"Item with id {response.Resource.GetType().GetProperty("id").GetValue(response.Resource, null)} created successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create item: {ex.Message}");
            }
        }
        // Add more methods for interacting with Cosmos DB as needed
    }
}
