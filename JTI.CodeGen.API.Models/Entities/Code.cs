﻿namespace JTI.CodeGen.API.Models.Entities
{
    public class Code
    {
        public string id { get; set; }
        public string code { get; set; }
        public string batch { get; set; }
        public string sequence { get; set; }
        public string lastupdate { get; set; }
        public string status { get; set; }

        public void UpdateCode(string newCode)
        {
            this.code = newCode;
        }
    }
}
