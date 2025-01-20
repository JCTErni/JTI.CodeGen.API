using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
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

            string batchNumber = CodeServiceHelper.GenerateBatchNumber(generateCodeRequest.Brand);

            var codes = new List<Code>();
            for (int i = 0; i < numberOfCodes; i++)
            {
                var code = new Code
                {
                    id = Guid.NewGuid().ToString(),
                    code = CodeServiceHelper.GenerateRandomCode(9),
                    batch = "1",
                    sequence = "1",
                    lastupdate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    status = "1",
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

    }
}
