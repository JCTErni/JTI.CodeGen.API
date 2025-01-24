using JTI.CodeGen.API.CodeModule.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Dtos.Exceptions
{
    public class BulkInsertException<T> : Exception
    {
        public List<T> InsertedCodes { get; }

        public BulkInsertException(string message, List<T> insertedCodes, Exception innerException)
            : base(message, innerException)
        {
            InsertedCodes = insertedCodes;
        }
    }
}