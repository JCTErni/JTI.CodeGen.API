using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JTI.CodeGen.API.CodeModule.Services;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.Models.Enums;
using Microsoft.Extensions.Logging;
using JTI.CodeGen.API.CodeModule.Helpers;
using Microsoft.Azure.Cosmos;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.Common.Helpers;


namespace JTI.CodeGen.API.CodeModule.Services
{
    public class CodeService : ICodeService
    {
        private readonly ILogger<CodeService> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public CodeService(ILogger<CodeService> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        public async Task<List<Code>> GenerateCodesAsync(int numberOfCodes, string brand)
        {
            string batchNumber = CodeServiceHelper.GenerateBatchNumber(brand);

            var codes = new List<Code>();
            for (int i = 0; i < numberOfCodes; i++)
            {
                var code = new Code
                {
                    id = Guid.NewGuid().ToString(),
                    Brand = brand,
                    BatchNumber = batchNumber,
                    EncryptedCode = CodeServiceHelper.GenerateEncryptedCode(),
                    DateCreated = DateTime.UtcNow.ToString(),
                    CreatedBy = "System",
                    DateUpdated = DateTime.UtcNow.ToString(),
                    UpdatedBy = "System",
                    Status = CodeStatusEnum.Pending.ToString(),
                };
                codes.Add(code);
                await _cosmosDbService.AddItemAsync<Code>(code, ConfigurationConstants.Brand1Container, new PartitionKey(code.BatchNumber));
            }
            return codes;
        }

        public async Task<List<Code>> GetAllCodesAsync()
        {
            var query = "SELECT * FROM c";
            var codes = new List<Code>();
            var iterator = _cosmosDbService.GetContainer(ConfigurationConstants.Brand1Container).GetItemQueryIterator<Code>(new QueryDefinition(query));

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                codes.AddRange(response);
            }

            return codes;
        }

        public List<Code> DecryptCodes(List<Code> encryptedCodes)
        {
            foreach (var encryptedCode in encryptedCodes)
            {
                encryptedCode.EncryptedCode = EncryptionHelper.Decrypt(encryptedCode.EncryptedCode);
            }
            return encryptedCodes;
        }
    }
}
