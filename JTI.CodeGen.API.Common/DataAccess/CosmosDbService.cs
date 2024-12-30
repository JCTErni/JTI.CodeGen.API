using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.Cosmos;
using JTI.CodeGen.API.Models.Entities;

namespace JTI.CodeGen.API.Common.DataAccess
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly ConcurrentDictionary<string, Container> _containers;

        public CosmosDbService(string connectionString, string databaseName)
        {
            _cosmosClient = new CosmosClient(connectionString);
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

        // Add more methods for interacting with Cosmos DB as needed
    }
}
