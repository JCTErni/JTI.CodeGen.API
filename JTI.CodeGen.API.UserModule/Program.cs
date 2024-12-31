using JTI.CodeGen.API.Common.DataAccess;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using Microsoft.Extensions.Configuration;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.UserModule.Dtos;
using JTI.CodeGen.API.Common.Services.Interfaces;
using JTI.CodeGen.API.Common.Services;
using JTI.CodeGen.API.Common.DataAccess.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using JTI.CodeGen.API.Common.Middleware;
using Microsoft.Extensions.Logging;
using JTI.CodeGen.API.UserModule.Services.Interfaces;
using JTI.CodeGen.API.UserModule.Services;

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

        services.AddSingleton<IUserDataAccess, UserDataAccess>();
        services.AddSingleton<IUserService, UserService>();

        services.AddAutoMapper((mapperConfiguration) =>
        {
            mapperConfiguration.CreateMap<CreateUserRequest, User>()
                .ForMember(dest => dest.HashedPassword, opt => opt.MapFrom(src => src.Password))
                .ForMember(dest => dest.id, opt => opt.Ignore());
            mapperConfiguration.CreateMap<User, UserDto>();
        });

        services.AddSingleton<IJwtService, JwtService>();
    })
    .Build();

await host.RunAsync();
