using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.Services;
using JTI.CodeGen.API.CodeModule.Services.Interfaces;
using JTI.CodeGen.API.Common.DataAccess;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Common.Middleware;
using JTI.CodeGen.API.Common.Services;
using JTI.CodeGen.API.Common.Services.Interfaces;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.Models.Entities;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
        worker.UseMiddleware<JwtMiddleware>();
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

        services.AddAutoMapper((mapperConfiguration) =>
        {
            mapperConfiguration.CreateMap<Code, CodeDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.id))
                .ForMember(dest => dest.Code, opt => opt.MapFrom(src => src.EncryptedCode));
        });

        services.AddSingleton<ICodeService, CodeService>();
        services.AddSingleton<IJwtService, JwtService>();
    })
    .Build();

await host.RunAsync();