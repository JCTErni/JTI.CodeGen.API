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
using JTI.CodeGen.API.Common.DataAccess.Interfaces;
using JTI.CodeGen.API.Models.Constants;

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
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString(), ApprRoleEnum.Printer.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            // Retrieve the encrypted codes from the database
            var encryptedCodes = await _codeService.GetAllCodesAsync();

            // Decrypt the codes
            var decryptedCodes = _codeService.DecryptCodes(encryptedCodes);

            // Map the decrypted codes to DTOs
            var codeDtos = _mapper.Map<List<CodeDto>>(decryptedCodes);

            // Create the response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDtos));

            return response;
        }
        
        [Function("get-codes-paginated")]
        public async Task<HttpResponseData> GetCodesPaginated([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get Codes Paginated] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString(), ApprRoleEnum.Printer.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            var pageSize = int.TryParse(req.Query[HttpParameterConstants.PageSize], out var ps) ? ps : ConfigurationConstants.defaultPageSize;

            var encryptedCodes = await _codeService.GetAllCodesAsync();

            var decryptedCodes = _codeService.DecryptCodes(encryptedCodes);

            var codeDtos = _mapper.Map<List<CodeDto>>(decryptedCodes);

            // Organize codes into a dictionary for pagination
            var codeDictionary = new Dictionary<int, List<CodeDto>>();
            int totalPages = (int)Math.Ceiling((double)codeDtos.Count() / pageSize);

            for (int i = 1; i <= totalPages; i++)
            {
                var pageCodes = codeDtos.Skip((i - 1) * pageSize).Take(pageSize).ToList();
                codeDictionary.Add(i, pageCodes);
            }

            // Create the response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDictionary));

            return response;
        }
        
        [Function("generate-codes")]
        public async Task<HttpResponseData> GenerateCode([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Generate Codes] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                _logger.LogWarning("[Generate Codes] Unauthorized access attempt.");
                return authResponse;
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("[Generate Codes] Request body read successfully.");
            var generateCodeRequest = JsonConvert.DeserializeObject<GenerateCodeRequest>(requestBody);
            _logger.LogInformation("[Generate Codes] Request deserialized successfully.");

            if (generateCodeRequest == null || generateCodeRequest.NumberOfCodes <= 0 || string.IsNullOrEmpty(generateCodeRequest.Brand))
            {
                _logger.LogWarning("[Generate Codes] Missing required parameters.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required parameters.");
                return badRequestResponse;
            }

            _logger.LogInformation("[Generate Codes] Generating codes for Brand: {Brand}, Number of Codes: {NumberOfCodes}", generateCodeRequest.Brand, generateCodeRequest.NumberOfCodes);
            await _codeService.GenerateCodesAsync(generateCodeRequest.NumberOfCodes, generateCodeRequest.Brand);

            _logger.LogInformation("[Generate Codes] Codes generated successfully.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Successfully generated {generateCodeRequest.NumberOfCodes} codes.");

            return response;
        }

        [Function("get-code-by-id")]
        public async Task<HttpResponseData> GetCodeById([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get Code By Id] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString(), ApprRoleEnum.Printer.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            // Ensure the query parameter name matches
            var id = req.Query[HttpParameterConstants.Id];

            // Retrieve the encrypted code by ID from the database
            var encryptedCode = await _codeService.GetCodeByIdAsync(id);

            if (encryptedCode == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Code with ID {id} not found.");
                return notFoundResponse;
            }

            // Decrypt the code
            encryptedCode.EncryptedCode = EncryptionHelper.Decrypt(encryptedCode.EncryptedCode);

            // Map the decrypted code to a DTO
            var codeDto = _mapper.Map<CodeDto>(encryptedCode);

            // Create the response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDto));

            return response;
        }
        //[Function("update-code-status")]
        //public async Task<HttpResponseData> UpdateCodeStatus([HttpTrigger(AuthorizationLevel.Function, "put")] HttpRequestData req, FunctionContext context)
        //{
        //    _logger.LogInformation("[Update User Status] Function Started.");

        //    // Define a list of valid roles
        //    var validRoles = new List<string> { ApprRoleEnum.SuperAdmin.ToString() };
        //    // Check if the user is authenticated and has a valid role
        //    if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
        //    {
        //        return authResponse;
        //    }

        //    // Parse the request body to get the new status
        //    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        //    var updateRequest = JsonConvert.DeserializeObject<UpdateCodeStatusRequest>(requestBody);

        //    if (updateRequest == null || string.IsNullOrEmpty(updateRequest.NewStatus))
        //    {
        //        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
        //        await badRequestResponse.WriteAsJsonAsync(new { message = "Invalid request body" });
        //        return badRequestResponse;
        //    }

        //    // Update the user status
        //    var updateResult = await _userDataAccess.UpdateUserStatusAsync(userId, updateRequest.NewStatus);

        //    if (!updateResult)
        //    {
        //        var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
        //        await notFoundResponse.WriteAsJsonAsync(new { message = "User not found" });
        //        return notFoundResponse;
        //    }

        //    var response = req.CreateResponse(HttpStatusCode.OK);
        //    await response.WriteAsJsonAsync(new { message = "User status updated successfully" });

        //    _logger.LogInformation("[Update User Status] Function Completed.");

        //    return response;
        //}

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
