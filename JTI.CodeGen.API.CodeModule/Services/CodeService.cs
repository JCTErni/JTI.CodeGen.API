using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.Models.Enums;
using Microsoft.Azure.Cosmos;

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

        public List<Code> GenerateCodesAsync(GenerateCodeRequest generateCodeRequest)
        {
            int numberOfCodes = generateCodeRequest.NumberOfCodes;
            string brand = generateCodeRequest.Brand;
            string printerName = generateCodeRequest.PrinterName;
            string printerAddress = generateCodeRequest.PrinterAddress;

            string batchNumber = CodeServiceHelper.GenerateBatchNumber(generateCodeRequest.Brand);

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
                    PrinterName = printerName,
                    PrinterAddress = printerAddress,
                };
                codes.Add(code);
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

        public async Task<Code> UpdateCodeStatusAsync(Code codeToUpdate, CodeStatusEnum newStatus)
        {
            // Update the status and DateUpdated
            codeToUpdate.Status = newStatus.ToString();
            codeToUpdate.DateUpdated = DateTime.UtcNow.ToString();

            // Replace the item in the container
            await _codeContainer.ReplaceItemAsync(codeToUpdate, codeToUpdate.id, new PartitionKey(codeToUpdate.EncryptedCode));
            return codeToUpdate;
        }
    }
}
