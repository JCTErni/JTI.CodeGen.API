using JTI.CodeGen.API.Models.Entities;

namespace JTI.CodeGen.API.Common.DataAccess.Interfaces
{
    public interface IUserDataAccess
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByUsernameAsync(string username);
        Task<User> AddUserAsync(User user);
    }
}
