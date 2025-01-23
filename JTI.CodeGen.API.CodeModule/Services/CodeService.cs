using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Cosmos;
using JTI.CodeGen.API.CodeModule.Enums;

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
            int codeLength = generateCodeRequest.CodeLength;
            int numberOfCodes = generateCodeRequest.NumberOfCodes;
            string batchNumber = generateCodeRequest.Batch;
            string sequence = generateCodeRequest.Sequence;
            var status = ((int)CodeStatusEnum.Generated).ToString();

            var codes = new List<Code>();
            for (int i = 0; i < numberOfCodes; i++)
            {
                string codeValue = CodeServiceHelper.GenerateRandomCode(codeLength);
                var code = new Code
                {
                    id =codeValue,
                    code = codeValue,
                    batch = batchNumber,
                    sequence = sequence,
                    status = status,
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
