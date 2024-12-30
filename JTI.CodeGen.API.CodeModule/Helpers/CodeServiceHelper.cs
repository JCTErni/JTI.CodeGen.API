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
        public static string GenerateEncryptedCode(string input)
        {
            return EncryptionHelper.Encrypt(input + Guid.NewGuid().ToString());
        }

        public static string GenerateBatchNumber(string brand)
        {
            return $"{brand}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }
    }
}
