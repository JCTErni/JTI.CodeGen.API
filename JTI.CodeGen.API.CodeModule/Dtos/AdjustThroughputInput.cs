using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class AdjustThroughputInput
    {
        public string ContainerId { get; set; }
        public int? Throughput { get; set; }
    }
}
