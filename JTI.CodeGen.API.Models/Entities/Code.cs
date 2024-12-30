using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JTI.CodeGen.API.Models.Enums;

namespace JTI.CodeGen.API.Models.Entities
{
    public class Code
    {
        public string id { get; set; }
        public string Brand { get; set; }
        public string BatchNumber { get; set; }
        public string HashedCode { get; set; }
        public string DateCreated { get; set; }
        public string CreatedBy { get; set; }
        public string DateUpdated { get; set; }
        public string UpdatedBy { get; set; }
        public string DateConsumed { get; set; }
        public string Status { get; set; }
        public string PrinterName { get; set; }
        public string PrinterAddress { get; set; }
    }
}
