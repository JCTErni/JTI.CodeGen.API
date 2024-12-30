using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Models.Dtos;

namespace FunctionApp3
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public Function1(ILogger<Function1> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("generate-code")]
        public async Task<HttpResponseData> GenerateCode([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            MyData data = JsonConvert.DeserializeObject<MyData>(requestBody);

            var response = req.CreateResponse(HttpStatusCode.OK);

            if (data != null)
            {
                var result = await _cosmosDbService.AddItemAsync<MyData>(data, data.categoryId);
                await response.WriteStringAsync($"Item added with id: {result.Resource.id}");
            }
            else
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid request data.");
            }
            return response;
        }
    }
}
