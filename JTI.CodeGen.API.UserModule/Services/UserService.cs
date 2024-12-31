using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JTI.CodeGen.API.Common.DataAccess.Interfaces;
using JTI.CodeGen.API.Models.Entities;
using JTI.CodeGen.API.UserModule.Services.Interfaces;

namespace JTI.CodeGen.API.UserModule.Services
{
    public class UserService : IUserService
    {
        private readonly IUserDataAccess _userDataAccess;

        public UserService(IUserDataAccess userDataAccess)
        {
            _userDataAccess = userDataAccess;
        }

        public Task<User> AddUserAsync(User user)
        {
            return _userDataAccess.AddUserAsync(user);
        }

        public Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return _userDataAccess.GetAllUsersAsync();
        }

        public Task<User> GetByEmailAsync(string email)
        {
            return _userDataAccess.GetByEmailAsync(email);
        }

        public Task<User> GetByUsernameAsync(string username)
        {
            return _userDataAccess.GetByUsernameAsync(username);
        }
    }
}
