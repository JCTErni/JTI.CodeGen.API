using System.Security.Claims;

namespace JTI.CodeGen.API.Common.Services.Interfaces
{
    public interface IJwtService
    {
        ClaimsPrincipal ValidateJWTToken(string token);
        string GenerateJwtToken(string username);
    }
}
