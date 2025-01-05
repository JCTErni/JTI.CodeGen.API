using JTI.CodeGen.API.Common.DataAccess.Interfaces;
using JTI.CodeGen.API.Models.Constants;
using Microsoft.Azure.Cosmos;

namespace JTI.CodeGen.API.Common.DataAccess
{
    public class UserDataAccess : IUserDataAccess
    {
        private readonly Container _userContainer;
        public UserDataAccess(CosmosDbService cosmosDbService)
        {
            _userContainer = cosmosDbService.GetContainer(ConfigurationConstants.UserContainer);
        }

        public async Task<IEnumerable<Models.Entities.User>> GetAllUsersAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _userContainer.GetItemQueryIterator<Models.Entities.User>(query);
            var users = new List<Models.Entities.User>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                users.AddRange(response);
            }

            return users;
        }

        public async Task<Models.Entities.User> GetByEmailAsync(string email)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Email = @Email")
                .WithParameter("@Email", email);

            var iterator = _userContainer.GetItemQueryIterator<Models.Entities.User>(query);
            var users = new List<Models.Entities.User>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                users.AddRange(response);
            }

            return users.FirstOrDefault();
        }

        public async Task<Models.Entities.User> GetByUsernameAsync(string username)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.UserName = @Username")
                .WithParameter("@Username", username);

            var iterator = _userContainer.GetItemQueryIterator<Models.Entities.User>(query);
            var users = new List<Models.Entities.User>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                users.AddRange(response);
            }

            return users.FirstOrDefault();
        }

        public async Task<Models.Entities.User> AddUserAsync(Models.Entities.User user)
        {
            // Create a hierarchical partition key
            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(user.Brand.ToString())
                .Add(user.AppRole.ToString())
                .Build();

            var response = await _userContainer.CreateItemAsync(user, partitionKey);
            return response.Resource;
        }
    }
}
