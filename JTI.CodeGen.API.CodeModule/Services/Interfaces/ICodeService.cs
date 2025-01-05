using JTI.CodeGen.API.CodeModule.Dtos;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.Models.Enums;

namespace JTI.CodeGen.API.CodeModule.Services.Interfaces
{
    public interface ICodeService
    {
        Task<List<Code>> GetAllCodesAsync();
        List<Code> GenerateCodesAsync(GenerateCodeRequest generateCodeRequest);
        Task<Code> GetCodeByIdAsync(string id);
        Task<Code> GetByCodeAsync(string code);
        List<Code> DecryptCodes(List<Code> encryptedCodes);
        Task<Code> UpdateCodeStatusAsync(Code codeToUpdate, CodeStatusEnum newStatus);
    }
}
