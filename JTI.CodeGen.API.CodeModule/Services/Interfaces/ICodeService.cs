using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.CodeModule.Entities;

namespace JTI.CodeGen.API.CodeModule.Services.Interfaces
{
    public interface ICodeService
    {
        Task<List<Code>> GetAllCodesAsync();
        List<Code> GenerateCodesAsync(GenerateCodeRequest generateCodeRequest);
    }
}
