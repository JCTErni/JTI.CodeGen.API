using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JTI.CodeGen.API.Models.Entities;

namespace JTI.CodeGen.API.Common.DataAccess.Interfaces
{
    public interface IUserDataAccess
    {
        Task<IEnumerable<User>> GetAllAsync(int pageNumber, int pageSize);
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByUsernameAsync(string username);
        Task<User> AddUserAsync(User user);
    }
}
