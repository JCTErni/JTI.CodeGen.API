namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class UpdateCodeStatusRequest
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string NewStatus { get; set; }
    }
}
