namespace JTI.CodeGen.API.Models.Entities
{
    public class CodeStatus
    {
        public string id { get; set; }
        public string EncryptedCode { get; set; }
        public DateTime? DateCreated { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? DateUpdated { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? DateConsumed { get; set; }
        public string Status { get; set; }
    }
}
