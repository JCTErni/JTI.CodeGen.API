using JTI.CodeGen.API.CodeModule.Entities;
namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class InsertCodesOrchestratorInput
    {
        public int CodeLength { get; set; }
        public string Batch { get; set; }
        public string Sequence { get; set; }
        public int BatchIndex {  get; set; }
        public int BatchSize { get; set; }
    }
}
