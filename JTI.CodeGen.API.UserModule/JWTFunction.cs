using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace JTI.CodeGen.API.UserModule
{
    public class JWTFunction
    {
        private readonly ILogger<JWTFunction> _logger;

        public JWTFunction(ILogger<JWTFunction> logger)
        {
            _logger = logger;
        }

        [Function("generate-jwt-secret-key")]
        public async Task<HttpResponseData> GenerateJWTSecretKey([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("[Generate Encryption Key] Function Started.");

            // Define the key length 
            int keyLength = 512 / 8; // 64 bytes

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
