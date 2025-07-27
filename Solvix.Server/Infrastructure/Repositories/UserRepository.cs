using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UserRepository : Repository<AppUser>, IUserRepository
    {
        private readonly ChatDbContext _chatDbContext;

        public UserRepository(ChatDbContext chatDbContext) : base(chatDbContext)
        {
            _chatDbContext = chatDbContext;
        }

        public async Task<AppUser?> GetByUsernameAsync(string username)
        {
            return await _chatDbContext.Users
                .FirstOrDefaultAsync(u => u.UserName == username);
        }

        public async Task<AppUser?> GetByPhoneNumberAsync(string phoneNumber)
        {
            return await _chatDbContext.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        public async Task<bool> PhoneNumberExistsAsync(string phoneNumber)
        {
            return await _chatDbContext.Users
                .AnyAsync(u => u.PhoneNumber == phoneNumber);
        }

        public async Task<List<AppUser>> SearchUsersAsync(string searchTerm, int limit = 20)
        {
            var trimmedTerm = searchTerm.Trim();

            return await _chatDbContext.Users
                .Where(u =>
                    (u.FirstName != null && u.FirstName.Contains(trimmedTerm)) ||
                    (u.LastName != null && u.LastName.Contains(trimmedTerm)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(trimmedTerm)) ||
                    ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(trimmedTerm))
                .Take(limit)
                .ToListAsync();
        }

        public async Task<IEnumerable<AppUser>> FindUsersByPhoneNumbersAsync(IEnumerable<string> phoneNumbers)
        {
            var normalizedPhoneNumbers = phoneNumbers.Where(p => p != null).Select(p => p.ToLower()).ToList(); // ✅ اصلاح CS8604
            return await _chatDbContext.Users
                                 .Where(u => u.PhoneNumber != null && normalizedPhoneNumbers.Contains(u.PhoneNumber.ToLower()))
                                 .ToListAsync();
        }
    }
}