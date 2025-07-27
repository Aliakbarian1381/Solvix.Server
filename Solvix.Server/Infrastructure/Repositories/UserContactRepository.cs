using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;
using System.Linq.Expressions;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class UserContactRepository : Repository<UserContact>, IUserContactRepository
    {
        private readonly new ChatDbContext _context;

        public UserContactRepository(ChatDbContext context) : base(context)
        {
            _context = context;
        }

        // ✅ متد اصلی که ChatHub استفاده می‌کنه
        public async Task<IEnumerable<UserContact>> GetUserContactsAsync(long userId)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && !uc.IsBlocked)
                .OrderByDescending(uc => uc.IsFavorite)
                .ThenBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                .ToListAsync();
        }

        public async Task<UserContact?> GetUserContactAsync(long ownerId, long contactId)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Include(uc => uc.OwnerUser)
                .FirstOrDefaultAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId);
        }

        public async Task<IEnumerable<UserContact>> GetContactsAsync(long ownerId, int limit = 50)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == ownerId && !uc.IsBlocked)
                .OrderByDescending(uc => uc.IsFavorite)
                .ThenBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<bool> RemoveContactAsync(long ownerId, long contactId)
        {
            try
            {
                var contact = await GetUserContactAsync(ownerId, contactId);
                if (contact != null)
                {
                    _context.UserContacts.Remove(contact);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task AddRangeAsync(IEnumerable<UserContact> contacts)
        {
            await _context.UserContacts.AddRangeAsync(contacts);
        }

        public async Task<IEnumerable<UserContact>> SearchContactsAsync(long userId, string searchTerm, int limit = 20)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && !uc.IsBlocked &&
                    (uc.ContactUser.FirstName!.Contains(searchTerm) ||
                     uc.ContactUser.LastName!.Contains(searchTerm) ||
                     uc.ContactUser.UserName!.Contains(searchTerm) ||
                     (uc.DisplayName != null && uc.DisplayName.Contains(searchTerm))))
                .Take(limit)
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

        public async Task<IEnumerable<UserContact>> GetRecentContactsAsync(long userId, int limit = 10)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && !uc.IsBlocked)
                .OrderByDescending(uc => uc.LastInteractionAt ?? uc.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserContact>> GetBlockedContactsAsync(long userId)
        {
            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId && uc.IsBlocked)
                .OrderBy(uc => uc.DisplayName ?? uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                .ToListAsync();
        }

        public async Task<bool> IsContactAsync(long ownerId, long contactId)
        {
            return await _context.UserContacts
                .AnyAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId);
        }

        public async Task<bool> IsBlockedAsync(long ownerId, long contactId)
        {
            return await _context.UserContacts
                .AnyAsync(uc => uc.OwnerUserId == ownerId && uc.ContactUserId == contactId && uc.IsBlocked);
        }

        public async Task<int> GetContactsCountAsync(long userId)
        {
            return await _context.UserContacts
                .CountAsync(uc => uc.OwnerUserId == userId && !uc.IsBlocked);
        }

        public async Task<int> GetFavoriteContactsCountAsync(long userId)
        {
            return await _context.UserContacts
                .CountAsync(uc => uc.OwnerUserId == userId && uc.IsFavorite && !uc.IsBlocked);
        }

        public async Task UpdateLastInteractionAsync(long ownerId, long contactId)
        {
            var contact = await GetUserContactAsync(ownerId, contactId);
            if (contact != null)
            {
                contact.LastInteractionAt = DateTime.UtcNow;
                _context.UserContacts.Update(contact);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<UserContact>> GetMutualContactsAsync(long userId1, long userId2)
        {
            var user1Contacts = await _context.UserContacts
                .Where(uc => uc.OwnerUserId == userId1 && !uc.IsBlocked)
                .Select(uc => uc.ContactUserId)
                .ToListAsync();

            var user2Contacts = await _context.UserContacts
                .Where(uc => uc.OwnerUserId == userId2 && !uc.IsBlocked)
                .Select(uc => uc.ContactUserId)
                .ToListAsync();

            var mutualContactIds = user1Contacts.Intersect(user2Contacts).ToList();

            return await _context.UserContacts
                .Include(uc => uc.ContactUser)
                .Where(uc => uc.OwnerUserId == userId1 && mutualContactIds.Contains(uc.ContactUserId))
                .ToListAsync();
        }

        public async Task<bool> UpdateContactsAsync(long ownerId, List<long> contactIds, Expression<Func<UserContact, UserContact>> updateExpression)
        {
            try
            {
                var contacts = await _context.UserContacts
                    .Where(uc => uc.OwnerUserId == ownerId && contactIds.Contains(uc.ContactUserId))
                    .ToListAsync();

                // فعلاً basic implementation - در آینده پیاده‌سازی کامل می‌شه
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public new IQueryable<UserContact> GetQueryable()
        {
            return _context.UserContacts.AsQueryable();
        }

        // ✅ Override متدهای base برای composite key
        public override async Task<UserContact> AddAsync(UserContact entity)
        {
            await _context.UserContacts.AddAsync(entity);
            return entity;
        }

        public override async Task UpdateAsync(UserContact entity)
        {
            _context.UserContacts.Update(entity);
            await Task.CompletedTask;
        }

        public override async Task DeleteAsync(UserContact entity)
        {
            _context.UserContacts.Remove(entity);
            await Task.CompletedTask;
        }

        public override async Task<UserContact?> GetByIdAsync(object id)
        {
            if (id is ValueTuple<long, long> compositeId)
            {
                return await GetUserContactAsync(compositeId.Item1, compositeId.Item2);
            }
            return null;
        }
    }
}