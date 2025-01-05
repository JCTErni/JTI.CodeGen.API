namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class GenerateCodeRequest
    {
        public int NumberOfCodes { get; set; }
        public string Brand { get; set; }
        public string PrinterName { get; set; }
        public string PrinterAddress { get; set; }
    }
}
