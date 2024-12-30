using JTI.CodeGen.API.Common.Helpers;
using JTI.CodeGen.API.Models.Constants;
using JTI.CodeGen.API.UserModule.Services.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.UserModule.Services
{
    public class JwtService : IJwtService
    {
        private readonly string _secretKey;
        private readonly string _issuer;

        public JwtService()
        {
            _secretKey = EnvironmentVariableHelper.GetValue(ConfigurationConstants.JWTSecret);
            _issuer = EnvironmentVariableHelper.GetValue(ConfigurationConstants.JWTIssuer);
        }

        public ClaimsPrincipal ValidateJWTToken(string token)
        {
            var tokenHandler = new JsonWebTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _issuer,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero // Adjust clock skew if necessary
            };

            try
            {
                var tokenValidationResult = tokenHandler.ValidateToken(token, validationParameters);
                if (!tokenValidationResult.IsValid)
                {
                    return null;
                }

                var jwtToken = tokenHandler.ReadJsonWebToken(token);
                var claims = jwtToken.Claims.ToList();

                return new ClaimsPrincipal(new ClaimsIdentity(claims));
            }
            catch
            {
                return null;
            }
        }

        public string GenerateJwtToken(string username)
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expirationTime = DateTime.UtcNow.AddHours(1);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
                Expires = expirationTime,
                Issuer = _issuer,
                Audience = _issuer,
                SigningCredentials = creds
            };

            var tokenHandler = new JsonWebTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return token;
        }
    }
}
