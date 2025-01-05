using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.Models.Constants
{
    public class ConfigurationConstants
    {
        public const string CosmosDbConnectionString = "CosmosDbConnectionString";
        public const string CosmosDbDatabaseName = "CosmosDbDatabaseName";
        public const string JWTSecret = "JWTSecret";
        public const string JWTIssuer = "JWTIssuer";
        public const string CodeEncryptionKey = "CodeEncryptionKey";
        public const string CodeContainer = "codes";
        public const string UserContainer = "users";
        public const string CodeStatusContainer = "codestatus";
        public const string BrandCodesContainer = "brand";
        public const string BatchCodesContainer = "batch";
        public const string StatusCodesContainer = "status";
        public const string PrinterCodesContainer = "printer";
        public const int defaultPageNumber = 1;
        public const int defaultPageSize = 10;
    }
}
