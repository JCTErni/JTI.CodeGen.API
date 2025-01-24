using JTI.CodeGen.API.CodeModule.Entities;

namespace JTI.CodeGen.API.CodeModule.Dtos
{
    public class CodeTriggerOrchestratorInput
    {
        public string ContainerId { get; set; }
        public int? OriginalMaxThroughput { get; set; }
        public int BatchSize { get; set; }
        public List<Code> Codes { get; set; }
    }
}
