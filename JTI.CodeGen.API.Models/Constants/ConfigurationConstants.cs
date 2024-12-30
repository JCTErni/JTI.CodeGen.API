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
        public const string Brand1Container = "BrandBCodes";
        public const string UserContainer = "Users";
        public const int defaultPageNumber = 1;
        public const int defaultPageSize = 10;
    }
}
