using JTI.CodeGen.API.CodeModule.Entities;
namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class InsertCodesOrchestratorInput
    {
        public string ContainerId { get; set; }
        public int? OriginalMaxThroughput { get; set; }
        public List<Code> CodeBatch { get; set; }
        public string Batch {  get; set; }
        public string Sequence { get; set; }
    }
}
