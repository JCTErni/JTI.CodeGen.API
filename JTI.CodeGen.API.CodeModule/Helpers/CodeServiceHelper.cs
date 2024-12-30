using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using JTI.CodeGen.API.Common.Helpers;

namespace JTI.CodeGen.API.CodeModule.Helpers
{
    public static class CodeServiceHelper
    {
        public static string GenerateEncryptedCode()
        {
            // Generate a 9-character alphanumeric code
            string randomCode = GenerateRandomCode(9);

            // Encrypt the generated code
            return EncryptionHelper.Encrypt(randomCode);
        }

        public static string GenerateRandomCode(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                                        .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string GenerateBatchNumber(string brand)
        {
            return $"{brand}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }
    }
}
