
using JTI.CodeGen.API.CodeModule.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Dtos.Exceptions
{
    public class CodeTriggerBatchInsertException : Exception
    {
        public List<Code> InsertedCodes { get; }

        public CodeTriggerBatchInsertException(string message, List<Code> insertedCodes, Exception innerException)
            : base(message, innerException)
        {
            InsertedCodes = insertedCodes;
        }
    }
}
