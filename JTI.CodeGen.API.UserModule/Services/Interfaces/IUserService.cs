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
