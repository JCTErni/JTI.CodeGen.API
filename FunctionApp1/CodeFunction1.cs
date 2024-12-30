using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using JTI.CodeGen.API.Common.DataAccess;

namespace FunctionApp1
{
    public class CodeFunction1
    {
        private readonly ILogger<CodeFunction1> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public CodeFunction1(ILogger<CodeFunction1> logger, CosmosDbService cosmosDbService)
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
            var data = JsonConvert.DeserializeObject<MyData>(requestBody);

            var response = req.CreateResponse(HttpStatusCode.OK);

            if (data != null)
            {
                var result = await _cosmosDbService.AddItemAsync(data, data.PartitionKey);
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
            public string PartitionKey { get; set; }
            // Add other properties as needed
        }
    }
}
