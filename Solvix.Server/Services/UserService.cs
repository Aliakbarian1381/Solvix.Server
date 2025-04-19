using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using Solvix.Server.Models;
using System.Threading.Tasks;


namespace Solvix.Server.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;


        public UserService(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }


        public async Task<AppUser> GetUserByUsername(string username)
        {
            return await _userManager.FindByNameAsync(username);
        }

        public async Task<AppUser> GetUserById(string id)
        {
            return await _userManager.FindByIdAsync(id);
        }

        public async Task<IdentityResult> CreateUserAsync(AppUser user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }
    }
}