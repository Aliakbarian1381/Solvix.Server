using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    namespace Solvix.Server.Core.Interfaces
    {
        public interface IUserContactRepository : IRepository<UserContact>
        {
            Task<IEnumerable<UserContact>> GetUserContactsWithDetailsAsync(long userId);
            Task<IEnumerable<UserContact>> GetFavoriteContactsAsync(long userId);
            Task<UserContact?> GetUserContactAsync(long ownerId, long contactId);
            Task<bool> ContactExistsAsync(long ownerId, long contactId);
            Task<int> GetContactCountAsync(long userId);
            Task<IEnumerable<UserContact>> GetRecentContactsAsync(long userId, int limit = 10);
            Task UpdateLastInteractionAsync(long ownerId, long contactId);
            Task<bool> RemoveContactAsync(long ownerId, long contactId);
            Task<IEnumerable<UserContact>> SearchContactsAsync(long userId, string searchTerm, int limit = 20);
            Task AddRangeAsync(IEnumerable<UserContact> contacts);
            IQueryable<UserContact> GetQueryable();
        }
    }
}