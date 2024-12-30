using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.UserModule.Helpers;
using JTI.CodeGen.API.UserModule.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.UserModule.Middleware
{
    public class JwtMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly IJwtService _jwtService;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(IJwtService jwtService, ILogger<JwtMiddleware> logger)
        {
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            if (context.BindingContext.BindingData.TryGetValue("Headers", out var headersObj))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersObj.ToString());
                if (headers != null && headers.TryGetValue("Authorization", out var token))
                {
                    if (token.StartsWith("Bearer "))
                    {
                        token = token.Substring(7);
                        var claimsPrincipal = _jwtService.ValidateJWTToken(token);
                        if (claimsPrincipal != null)
                        {
                            context.Items["User"] = claimsPrincipal.Claims.ToDictionary(c => c.Type, c => c.Value);
                        }
                    }
                }
            }
            await next(context);
        }
    }
}
