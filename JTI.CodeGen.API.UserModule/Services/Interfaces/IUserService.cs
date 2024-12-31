using Microsoft.AspNetCore.Identity.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JTI.CodeGen.API.UserModule.Dtos;
using JTI.CodeGen.API.Models.Entities;

namespace JTI.CodeGen.API.UserModule.Services.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByUsernameAsync(string username);
        Task<User> AddUserAsync(User user);
    }
}
