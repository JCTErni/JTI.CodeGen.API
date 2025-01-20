using JTI.CodeGen.API.CodeModule.Services;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.CodeModule.DataAccess;
using JTI.CodeGen.API.CodeModule.Helpers;
using JTI.CodeGen.API.CodeModule.Constants;
using JTI.CodeGen.API.CodeModule.Entities;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<CosmosDbService>(sp =>
        {
            var connString = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbConnectionString);
            var databaseName = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbDatabaseName);

            if (string.IsNullOrEmpty(connString) || string.IsNullOrEmpty(databaseName))
            {
                throw new InvalidOperationException("Cosmos DB settings cannot be null or empty.");
            }

            return new CosmosDbService(connString, databaseName);
        });

        services.AddSingleton<ICodeService, CodeService>();
    })
    .Build();

await host.RunAsync();