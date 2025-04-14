using Microsoft.AspNetCore.Identity;
using Solvix.Server.Models;
using System.Threading.Tasks;


namespace Solvix.Server.Services

{
    public interface IUserService
    {
        Task<AppUser> GetUserByUsername(string username);
        Task<AppUser> GetUserById(string id);
        Task<IdentityResult> CreateUserAsync(AppUser user, string password);


    }
}
