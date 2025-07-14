using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IUserService
    {
        // Core user operations
        Task<AppUser?> GetUserByUsernameAsync(string username);
        Task<AppUser?> GetUserByIdAsync(long userId);
        Task<IdentityResult> CreateUserAsync(AppUser user, string password);
        Task<bool> CheckPhoneExistsAsync(string phoneNumber);

        // Search and discovery
        Task<List<UserDto>> SearchUsersAsync(string searchTerm, long currentUserId, int limit = 20);

        // Contact management - متدهای اصلی
        Task<IEnumerable<UserDto>> FindUsersByPhoneNumbersAsync(IEnumerable<string> phoneNumbers, long currentUserId);
        Task<IEnumerable<UserDto>> GetSavedContactsAsync(long ownerUserId);
        Task<IEnumerable<UserDto>> GetSavedContactsWithChatInfoAsync(long ownerUserId);

        // Contact management - متدهای جدید
        Task<bool> MarkContactAsFavoriteAsync(long ownerId, long contactId, bool isFavorite);
        Task<bool> BlockContactAsync(long ownerId, long contactId, bool isBlocked);
        Task<bool> UpdateContactDisplayNameAsync(long ownerId, long contactId, string? displayName);
        Task<bool> RemoveContactAsync(long ownerId, long contactId);

        // Contact search and filter
        Task<IEnumerable<UserDto>> SearchContactsAsync(long userId, string searchTerm, int limit = 20);
        Task<IEnumerable<UserDto>> GetFavoriteContactsAsync(long userId);
        Task<IEnumerable<UserDto>> GetRecentContactsAsync(long userId, int limit = 10);

        // Statistics and counts - متدهای جدید اضافه شده
        Task<int> GetContactsCountAsync(long userId);
        Task<int> GetFavoriteContactsCountAsync(long userId);
        Task<int> GetBlockedContactsCountAsync(long userId);

        // Advanced operations - متدهای پیشرفته
        Task<IEnumerable<UserDto>> GetFilteredContactsAsync(long userId, bool? isFavorite, bool? isBlocked, bool? hasChat, string sortBy, string sortDirection);
        Task<bool> BatchUpdateContactsAsync(long ownerId, List<long> contactIds, Dictionary<string, object> updates);
        Task<IEnumerable<UserDto>> GetMutualContactsAsync(long userId1, long userId2);
        Task<ContactImportResult> ImportContactsAsync(long userId, List<ImportContactItem> contacts);
        Task<byte[]> ExportContactsAsync(long userId, string format);
        Task<bool> UpdateSyncStatusAsync(long userId, DateTime lastSyncTime, int syncedCount);

        // User status and activity
        Task<bool> UpdateUserLastActiveAsync(long userId);
        Task<bool> UpdateFcmTokenAsync(long userId, string fcmToken);
        Task<bool> UpdateLastInteractionAsync(long ownerId, long contactId);
    }

    // Helper classes که باید اینجا تعریف بشن
    public class ContactImportResult
    {
        public int ImportedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public class ImportContactItem
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public bool IsFavorite { get; set; }
    }
}
