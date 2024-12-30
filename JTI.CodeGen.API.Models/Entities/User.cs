using JTI.CodeGen.API.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTI.CodeGen.API.Models.Entities
{
    public class User
    {
        public string id { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string HashedPassword { get; set; }
        public string Brand { get; set; }
        public string AppRole { get; set; }
    }
}
