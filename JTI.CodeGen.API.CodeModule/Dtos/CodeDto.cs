using JTI.CodeGen.API.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class CodeDto
    {
        public string Id { get; set; }
        public string Brand { get; set; }
        public string BatchNumber { get; set; }
        public string Code { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime DateConsumed { get; set; }
        public CodeStatusEnum Status { get; set; }
        public string PrinterName { get; set; }
        public string PrinterAddress { get; set; }
    }
}
