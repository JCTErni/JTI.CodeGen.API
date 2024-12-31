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
using System.Net;


namespace JTI.CodeGen.API.CodeModule.Services
{
    public class CodeService : ICodeService
    {
        private readonly Container _codeContainer;

        public CodeService(CosmosDbService cosmosDbService)
        {
            _codeContainer = cosmosDbService.GetContainer(ConfigurationConstants.CodeContainer);
        }

        public async Task<List<Code>> GetAllCodesAsync()
        {
            var query = "SELECT * FROM c";
            var iterator = _codeContainer.GetItemQueryIterator<Code>(query);
            var codes = new List<Code>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                codes.AddRange(response);
            }
            return codes;
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
                await _codeContainer.CreateItemAsync(code, new PartitionKey(code.BatchNumber));
            }
            return codes;
        }
        
        public async Task<Code> GetCodeByIdAsync(string id)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id);
            var iterator = _codeContainer.GetItemQueryIterator<Code>(query);
            var codes = new List<Code>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                codes.AddRange(response);
            }
            return codes.FirstOrDefault();
        }

        public async Task<Code> GetByCodeAsync(string code)
        {
            var encryptedCode = EncryptionHelper.Encrypt(code);
            var query = new QueryDefinition("SELECT * FROM c WHERE c.EncryptedCode = @encryptedCode")
                .WithParameter("@encryptedCode", encryptedCode);

            var iterator = _codeContainer.GetItemQueryIterator<Code>(query);
            var codes = new List<Code>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                codes.AddRange(response);
            }

            return codes.FirstOrDefault();
        }

        public List<Code> DecryptCodes(List<Code> encryptedCodes)
        {
            foreach (var encryptedCode in encryptedCodes)
            {
                encryptedCode.EncryptedCode = EncryptionHelper.Decrypt(encryptedCode.EncryptedCode);
            }
            return encryptedCodes;
        }

        //public async Task<bool> UpdateCodeStatusAsync(string codeId, string newStatus)
        //{
        //    var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @codeId")
        //        .WithParameter("@codeId", codeId);

        //    var iterator = _codeContainer.GetItemQueryIterator<Code>(query);
        //    Code code = null;

        //    while (iterator.HasMoreResults)
        //    {
        //        var response = await iterator.ReadNextAsync();
        //        code = response.FirstOrDefault();
        //        if (code != null)
        //        {
        //            break;
        //        }
        //    }

        //    if (code == null)
        //    {
        //        return false;
        //    }

        //    // Update the status
        //    code.Status = newStatus;

        //    // Replace the item in the container
        //    await container.ReplaceItemAsync(code, code.Id, new PartitionKey(code.PartitionKey));
        //    return true;
        //}
    }
}
