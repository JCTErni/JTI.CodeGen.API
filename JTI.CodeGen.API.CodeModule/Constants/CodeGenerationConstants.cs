using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Constants
{
    public class CodeGenerationConstants
    {
        public const int requestUnitPerItem = 23;
        public const int maxThroughputAdjustment = 10000;
        public const int batchSize = 10000;
        public const int insertRetryTime = 1; // in seconds
        public const int maxInsertRetryTime = 3;
    }
}
