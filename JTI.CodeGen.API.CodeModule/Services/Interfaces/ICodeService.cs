using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JTI.CodeGen.API.Models.Entities;

namespace JTI.CodeGen.API.CodeModule.Services.Interfaces
{
    public interface ICodeService
    {
        Task<List<Code>> GenerateCodesAsync(int numberOfCodes, string brand);
        Task<List<Code>> GetAllCodesAsync();
        List<Code> DecryptCodes(List<Code> encryptedCodes);
    }
}
