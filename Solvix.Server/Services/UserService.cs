using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using Solvix.Server.Models;

namespace Solvix.Server.Services
{
    public class UserService : IUserService
    {
        private readonly ChatDbContext _context;

        public UserService(ChatDbContext context)
        {
            _context = context;
        }

        public async Task<User> GetUserByUsername(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User> GetUserById(int id)
        {
            return await _context.Users.FindAsync(id);
        }

    }
}
