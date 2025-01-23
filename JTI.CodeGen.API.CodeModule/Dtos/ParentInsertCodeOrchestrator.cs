using JTI.CodeGen.API.CodeModule.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class ParentInsertCodeOrchestratorInput
    {
        public string ContainerId { get; set; }
        public int? OriginalMaxThroughput { get; set; }
        public int NumberOfCodes { get; set; }
        public int CodeLength { get; set; }
        public int BatchSize { get; set; }
        public int TotalBatches { get; set; }
        public string Batch { get; set; }
        public string Sequence { get; set; }
    }
}
