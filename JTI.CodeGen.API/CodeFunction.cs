using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using JTI.CodeGen.API.Common.DataAccess;

namespace JTI.CodeGen.API.CodeModule
{
    public class CodeFunction
    {
        private readonly ILogger<CodeFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public CodeFunction(ILogger<CodeFunction> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("generate-code")]
        public async Task<HttpResponseData> GenerateCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            MyData data = JsonConvert.DeserializeObject<MyData>(requestBody);

            var response = req.CreateResponse(HttpStatusCode.OK);

            if (data != null)
            {
                var result = await _cosmosDbService.AddItemAsync(data, data.categoryId);
                await response.WriteStringAsync($"Item added with id: {result.Resource.Id}");
            }
            else
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid request data.");
            }

            return response;
        }

        public class MyData
        {
            public string Id { get; set; }
            public string categoryId { get; set; }
            // Add other properties as needed
        }

        [Function("Function1")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            var response = req.CreateResponse(HttpStatusCode.OK);

            return response;
        }
    }
}