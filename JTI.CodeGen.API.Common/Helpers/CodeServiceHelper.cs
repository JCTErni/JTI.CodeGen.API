namespace JTI.CodeGen.API.Common.Helpers
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
            // Generate a new GUID
            Guid guid = Guid.NewGuid();

            // Convert the GUID to a byte array
            byte[] guidBytes = guid.ToByteArray();

            // Encode the byte array using Base64
            string base64String = Convert.ToBase64String(guidBytes);

            // Remove non-alphanumeric characters and take the first 9 characters
            string uniqueCode = new string(base64String.Where(char.IsLetterOrDigit).ToArray());

            // Ensure the code is exactly 9 characters long
            if (uniqueCode.Length > length)
            {
                uniqueCode = uniqueCode.Substring(0, length);
            }
            else if (uniqueCode.Length < length)
            {
                // Recursively generate additional characters to meet the desired length
                uniqueCode += GenerateRandomCode(length - uniqueCode.Length);
            }

            return uniqueCode.ToUpper(); // Convert to uppercase for consistency
        }

        public static string GenerateBatchNumber(string brand)
        {
            // Incorporate a GUID to ensure uniqueness
            string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{DateTime.UtcNow:yyyyMMddHHmmss}{guidPart}";
        }
    }
}
