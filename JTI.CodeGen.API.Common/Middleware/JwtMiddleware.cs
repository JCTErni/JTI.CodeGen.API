using JTI.CodeGen.API.Common.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace JTI.CodeGen.API.Common.Middleware
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
