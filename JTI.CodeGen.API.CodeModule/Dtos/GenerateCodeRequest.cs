namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class GenerateCodeRequest
    {
        public int NumberOfCodes { get; set; }
        public int CodeLength { get; set; }
        public string Brand { get; set; }
        public string Batch { get; set; }
        public string Sequence { get; set; }
    }
}
