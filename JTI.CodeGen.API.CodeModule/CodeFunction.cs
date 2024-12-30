using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.CodeModule.Dtos;
using AutoMapper;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeFunction
    {
        private readonly ILogger<CodeFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly ICodeService _codeService;
        private readonly IMapper _mapper;

        public CodeFunction(ILogger<CodeFunction> logger, CosmosDbService cosmosDbService, ICodeService codeService, IMapper mapper)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _codeService = codeService;
            _mapper = mapper;
        }

        [Function("generate-codes")]
        public async Task<HttpResponseData> GenerateCode([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            GenerateCodeRequest data = JsonConvert.DeserializeObject<GenerateCodeRequest>(requestBody);

            var response = req.CreateResponse(HttpStatusCode.OK);

            if (data != null && data.NumberOfCodes > 0 && !string.IsNullOrEmpty(data.Brand))
            {
                var codes = await _codeService.GenerateCodesAsync(data.NumberOfCodes, data.Brand);
                await response.WriteStringAsync($"Generated {data.NumberOfCodes} codes for brand: {data.Brand}");
            }
            else
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid request data.");
            }
            return response;
        }

        [Function("get-all-codes")]
        public async Task<HttpResponseData> GetAllCodes([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to get all codes.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            var codes = await _codeService.GetAllCodesAsync();

            var codeDtos = _mapper.Map<List<CodeDto>>(codes);

            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDtos));

            return response;
        }
    }
}
