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
        public string batchNumber { get; set; }
        public string HashedCode { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
        public string UpdatedBy { get; set; }
        public CodeStatusEnum Status { get; set; }
    }
}
