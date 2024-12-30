using JTI.CodeGen.API.Common.DataAccess;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using Microsoft.Extensions.Configuration;

var builder = FunctionsApplication.CreateBuilder(args);
// Configure services
builder.Services.AddSingleton<CosmosDbService>(sp =>
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

    return new CosmosDbService(connString, databaseName, containerName);
});

// Configure application configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Build and run the application
var app = builder.Build();
app.Run();
