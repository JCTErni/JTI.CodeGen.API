using System.Text.Json;

namespace JTI.CodeGen.API.Common.Helpers
{
    public static class EnvironmentVariableHelper
    {
        public static string GetValue(string environmentVariableName)
        {
            return Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);
        }

        public static bool GetBooleanValue(string environmentVariableName)
        {
            string value = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);
            return bool.TryParse(value, out bool result) ? result : false;
        }

        public static IDictionary<string, string> GetDictionaryValues(string environmentVariableName)
        {
            string json = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Environment variable '{environmentVariableName}' is not set or is empty.");
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
    }
}
