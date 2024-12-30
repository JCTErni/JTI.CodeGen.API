using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.UserModule.Services.Interfaces
{
    public interface IJwtService
    {
        ClaimsPrincipal ValidateJWTToken(string token);
        string GenerateJwtToken(string username);
    }
}
