using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace JTI.CodeGen.API.CodeModule.Helpers
{
    public static class CodeServiceHelper
    {
        public static string GenerateHashedCode(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input + Guid.NewGuid().ToString()));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string GenerateBatchNumber(string brand)
        {
            return $"{brand}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }
    }
}
