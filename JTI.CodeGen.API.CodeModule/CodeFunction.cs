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
using Microsoft.IdentityModel.Tokens;

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
            await response.WriteAsJsonAsync(codeDtos);

            _logger.LogInformation("[Get All Codes] Function Completed.");

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
            await response.WriteAsJsonAsync(codeDictionary);

            _logger.LogInformation("[Get Codes Paginated] Function Completed.");

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

            if (generateCodeRequest == null || generateCodeRequest.NumberOfCodes <= 0 || string.IsNullOrEmpty(generateCodeRequest.Brand) || string.IsNullOrEmpty(generateCodeRequest.PrinterName) || string.IsNullOrEmpty(generateCodeRequest.PrinterAddress))
            {
                _logger.LogWarning("[Generate Codes] Missing required parameters.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Missing required parameters.");
                return badRequestResponse;
            }

            _logger.LogInformation("[Generate Codes] Generating codes for Brand: {Brand}, Number of Codes: {NumberOfCodes}", generateCodeRequest.Brand, generateCodeRequest.NumberOfCodes);
            await _codeService.GenerateCodesAsync(generateCodeRequest);

            _logger.LogInformation("[Generate Codes] Codes generated successfully.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Successfully generated {generateCodeRequest.NumberOfCodes} codes.");

            _logger.LogInformation("[Generate Codes] Function Completed.");

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
            if (string.IsNullOrEmpty(id))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("id query parameter is missing.");
                return badRequestResponse;
            }

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
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDto));

            _logger.LogInformation("[Get Code By Id] Function Completed.");

            return response;
        }

        [Function("get-by-code")]
        public async Task<HttpResponseData> GetByCode([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Get By Code] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString(), ApprRoleEnum.Printer.ToString() };
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            // Ensure the query parameter name matches
            var code = req.Query[HttpParameterConstants.Code];
            if (string.IsNullOrEmpty(code))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Code query parameter is missing.");
                return badRequestResponse;
            }

            // Retrieve the encrypted code by the provided code from the database
            var encryptedCode = await _codeService.GetByCodeAsync(code);

            if (encryptedCode == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Code {code} not found.");
                return notFoundResponse;
            }

            // Decrypt the code
            encryptedCode.EncryptedCode = EncryptionHelper.Decrypt(encryptedCode.EncryptedCode);

            // Map the decrypted code to a DTO
            var codeDto = _mapper.Map<CodeDto>(encryptedCode);

            // Create the response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonConvert.SerializeObject(codeDto));

            _logger.LogInformation("[Get By Code] Function Completed.");

            return response;
        }

        [Function("update-code-status")]
        public async Task<HttpResponseData> UpdateCodeStatus([HttpTrigger(AuthorizationLevel.Function, "put")] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("[Update Code Status] Function Started.");

            // Define a list of valid roles
            var validRoles = new List<string> { ApprRoleEnum.Admin.ToString()};
            // Check if the user is authenticated and has a valid role
            if (await AuthHelper.CheckAuthenticationAndAuthorization(context, req, validRoles) is HttpResponseData authResponse)
            {
                return authResponse;
            }

            // Parse the request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updateCodeStatusRequest = JsonConvert.DeserializeObject<UpdateCodeStatusRequest>(requestBody);

            // Validate that at least one of Id or Code has a value
            if (string.IsNullOrEmpty(updateCodeStatusRequest.Id) && string.IsNullOrEmpty(updateCodeStatusRequest.Code))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Either Id or Code must be provided.");
                return badRequestResponse;
            }

            // Validate the new status
            if (!Enum.TryParse<CodeStatusEnum>(updateCodeStatusRequest.NewStatus, out var newStatus))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid status value.");
                return badRequestResponse;
            }

            // Retrieve the code from the database
            var codeToUpdate = updateCodeStatusRequest.Id == null ? 
                await _codeService.GetCodeByIdAsync(updateCodeStatusRequest.Id) :
                await _codeService.GetByCodeAsync(updateCodeStatusRequest.Code);

            if (codeToUpdate == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Code not found.");
                return notFoundResponse;
            }

            // Check if the new status is different from the current status
            if (codeToUpdate.Status == newStatus.ToString())
            {
                var okResponse = req.CreateResponse(HttpStatusCode.OK);
                await okResponse.WriteStringAsync($"Status is already {newStatus.ToString()}");
                return okResponse;
            }

            // Update the code status
            var updatedCode = await _codeService.UpdateCodeStatusAsync(codeToUpdate, newStatus);

            if (updatedCode == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Code not found or update failed.");
                return notFoundResponse;
            }

            // Create the response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Status updated successfully.");

            _logger.LogInformation("[Update Code Status] Function Completed.");

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
