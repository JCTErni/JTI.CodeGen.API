using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostBuilderContext, services) =>
    {
        // Retrieve the Cosmos DB connection string and other settings from environment variables
        var connString = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbConnectionString);
        var databaseName = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbDatabaseName);
        var containerName = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbContainerName);

        // Check if the connection string and other settings are not null or empty
        if (string.IsNullOrEmpty(connString) || string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(containerName))
        {
            throw new InvalidOperationException("Cosmos DB settings cannot be null or empty.");
        }

        // Register CosmosDbService with dependency injection
        services.AddSingleton<CosmosDbService>(sp =>
        {
            return new CosmosDbService(connString, databaseName, containerName);
        });
    })
    .ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) =>
    {
        hostBuilderContext.Configuration = configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    })
    .Build();

hostBuilder.Run();
