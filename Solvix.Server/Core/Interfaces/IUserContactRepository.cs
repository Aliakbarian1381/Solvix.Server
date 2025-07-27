using Solvix.Server.Core.Entities;
using System.Linq.Expressions;

namespace Solvix.Server.Core.Interfaces
{
    public interface IUserContactRepository : IRepository<UserContact>
    {
        Task<IEnumerable<UserContact>> GetUserContactsAsync(long userId);

        // Basic CRUD operations
        Task<UserContact?> GetUserContactAsync(long ownerId, long contactId);
        Task<IEnumerable<UserContact>> GetContactsAsync(long ownerId, int limit = 50);
        Task<bool> RemoveContactAsync(long ownerId, long contactId);
        Task AddRangeAsync(IEnumerable<UserContact> contacts);

        // Search and filter
        Task<IEnumerable<UserContact>> SearchContactsAsync(long userId, string searchTerm, int limit = 20);
        Task<IEnumerable<UserContact>> GetFavoriteContactsAsync(long userId);
        Task<IEnumerable<UserContact>> GetBlockedContactsAsync(long userId);
        Task<IEnumerable<UserContact>> GetRecentContactsAsync(long userId, int limit = 10);

        // Status checks
        Task<bool> IsContactAsync(long ownerId, long contactId);
        Task<bool> IsBlockedAsync(long ownerId, long contactId);

        // Statistics
        Task<int> GetContactsCountAsync(long userId);
        Task<int> GetFavoriteContactsCountAsync(long userId);

        // Interactions
        Task UpdateLastInteractionAsync(long ownerId, long contactId);
        Task<IEnumerable<UserContact>> GetMutualContactsAsync(long userId1, long userId2);

        // Batch operations
        Task<bool> UpdateContactsAsync(long ownerId, List<long> contactIds, Expression<Func<UserContact, UserContact>> updateExpression);

        // Query building - اصلاح warning CS0108
        new IQueryable<UserContact> GetQueryable();
    }
}