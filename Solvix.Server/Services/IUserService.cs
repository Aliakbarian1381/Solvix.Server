using Solvix.Server.Models;


namespace Solvix.Server.Services

{
    public interface IUserService
    {
        Task<User> GetUserByUsername(string username);
        Task<User> GetUserById(int userId);
    }
}
