using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace JTI.CodeGen.API.Common.Helpers
{
    public static class AuthHelper
    {
        public static async Task<(IDictionary<string, string> userClaims, HttpResponseData response)> CheckUserAuthentication(FunctionContext context, HttpRequestData req)
        {
            if (!context.Items.TryGetValue("User", out var userObj) || userObj is not IDictionary<string, string> userClaims)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return (null, unauthorizedResponse);
            }
            return (userClaims, null);
        }

        public static async Task<HttpResponseData> CheckUserRole(IDictionary<string, string> userClaims, HttpRequestData req, List<string> validRoles)
        {
            if (!userClaims.TryGetValue("AppRole", out var appRole) || !validRoles.Contains(appRole))
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteStringAsync($"Forbidden: You do not have permission to perform this action.");
                return forbiddenResponse;
            }
            return null;
        }

        public static async Task<HttpResponseData> CheckAuthenticationAndAuthorization(FunctionContext context, HttpRequestData req, List<string> validRoles)
        {
            var (userClaims, authResponse) = await CheckUserAuthentication(context, req);
            if (authResponse != null)
            {
                return authResponse;
            }

            var roleResponse = await CheckUserRole(userClaims, req, validRoles);
            if (roleResponse != null)
            {
                return roleResponse;
            }

            return null;
        }
    }
}
