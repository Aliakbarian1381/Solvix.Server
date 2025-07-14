using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces.Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UserContactRepository : Repository<UserContact>, IUserContactRepository
    {
        private readonly ChatDbContext _context;

        public UserContactRepository(ChatDbContext chatDbContext) : base(chatDbContext)
        {
            _context = chatDbContext;
        }

        public async Task<IEnumerable<UserContact>> GetUserContactsWithDetailsAsync(long userId)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Include(uc => uc.OwnerUser)
                .Where(uc => uc.OwnerUserId == userId && !uc.IsBlocked)
                .OrderBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserContact>> GetFavoriteContactsAsync(long userId)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && uc.IsFavorite && !uc.IsBlocked)
                .OrderBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                .ToListAsync();
        }

        public async Task<UserContact?> GetUserContactAsync(long ownerId, long contactId)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Include(uc => uc.OwnerUser)
                .FirstOrDefaultAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId);
        }

        public async Task<bool> ContactExistsAsync(long ownerId, long contactId)
        {
            return await _context.UserContacts
                .AnyAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId);
        }

        public async Task<int> GetContactCountAsync(long userId)
        {
            return await _context.UserContacts
                .CountAsync(uc => uc.OwnerUserId == userId && !uc.IsBlocked);
        }

        public async Task<IEnumerable<UserContact>> GetRecentContactsAsync(long userId, int limit = 10)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && !uc.IsBlocked)
                .OrderByDescending(uc => uc.LastInteractionAt ?? uc.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task UpdateLastInteractionAsync(long ownerId, long contactId)
        {
            var contact = await _context.UserContacts
                .FirstOrDefaultAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId);

            if (contact != null)
            {
                contact.LastInteractionAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> RemoveContactAsync(long ownerId, long contactId)
        {
            var contact = await _context.UserContacts
                .FirstOrDefaultAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId);

            if (contact != null)
            {
                _context.UserContacts.Remove(contact);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<UserContact>> SearchContactsAsync(long userId, string searchTerm, int limit = 20)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && !uc.IsBlocked &&
                    (uc.ContactUser.FirstName!.Contains(searchTerm) ||
                     uc.ContactUser.LastName!.Contains(searchTerm) ||
                     uc.ContactUser.UserName.Contains(searchTerm) ||
                     uc.ContactUser.PhoneNumber!.Contains(searchTerm) ||
                     (uc.DisplayName != null && uc.DisplayName.Contains(searchTerm))))
                .OrderBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                .Take(limit)
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<UserContact> contacts)
        {
            await _context.UserContacts.AddRangeAsync(contacts);
        }

        public IQueryable<UserContact> GetQueryable()
        {
            return _context.UserContacts.AsQueryable();
        }
    }
}