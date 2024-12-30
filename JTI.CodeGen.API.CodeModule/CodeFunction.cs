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
using JTI.CodeGen.API.Common.Services.Interfaces;
using JTI.CodeGen.API.Models.Enums;
using JTI.CodeGen.API.Common.Helpers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeFunction
    {
        private readonly ILogger<CodeFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly ICodeService _codeService;
        private readonly IMapper _mapper;
        private readonly IJwtService _jwtService;

        public CodeFunction(ILogger<CodeFunction> logger, CosmosDbService cosmosDbService, ICodeService codeService, IMapper mapper, IJwtService jwtService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _codeService = codeService;
            _mapper = mapper;
            _jwtService = jwtService;
        }

        [Function("get-all-codes")]
        public async Task<HttpResponseData> GetAllCodes([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get All Codes] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            var result = await _codeService.GetAllCodesAsync();

            var codeDtos = _mapper.Map<List<CodeDto>>(result);

            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDtos));

            return response;
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

        [Function("generate-encryption-key")]
        public async Task<HttpResponseData> GenerateEncryptionKey([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("[Generate Encryption Key] Function Started.");

            // Define the key length (e.g., 256 bits for AES-256)
            int keyLength = 256 / 8; // 32 bytes

            // Generate a secure random key
            byte[] key = new byte[keyLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }

            // Convert the key to a base64 string for easier handling
            string base64Key = Convert.ToBase64String(key);

            // Create the response
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            // Write the key to the response body as JSON
            var responseBody = new { Key = base64Key };
            await response.WriteStringAsync(JsonConvert.SerializeObject(responseBody));

            return response;
        }
    }
}
