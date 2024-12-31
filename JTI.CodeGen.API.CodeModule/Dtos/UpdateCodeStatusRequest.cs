using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class UpdateCodeStatusRequest
    {
        public string id {  get; set; }
        public string Code { get; set; }
        public string NewStatus { get; set; }
    }
}
