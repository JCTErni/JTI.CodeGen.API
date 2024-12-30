using JTI.CodeGen.API.Common.DataAccess;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using Microsoft.Extensions.Configuration;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.CodeModule.Services;
using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.Models.Entities;

var builder = FunctionsApplication.CreateBuilder(args);
// Configure services
builder.Services.AddSingleton<CosmosDbService>(sp =>
{
    // Retrieve the Cosmos DB connection string and other settings from environment variables
    var connString = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbConnectionString);
    var databaseName = EnvironmentVariableHelper.GetValue(ConfigurationConstants.CosmosDbDatabaseName);

    // Check if the connection string and other settings are not null or empty
    if (string.IsNullOrEmpty(connString) || string.IsNullOrEmpty(databaseName))
    {
        throw new InvalidOperationException("Cosmos DB settings cannot be null or empty.");
    }

    return new CosmosDbService(connString, databaseName);
});

// Register the CodeService
builder.Services.AddSingleton<ICodeService, CodeService>();

// Register AutoMapper and configure mappings
builder.Services.AddAutoMapper((mapperConfiguration) =>
{
    mapperConfiguration.CreateMap<Code, CodeDto>()
        .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.id));
});

// Configure application configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Build and run the application
var app = builder.Build();
app.Run();
