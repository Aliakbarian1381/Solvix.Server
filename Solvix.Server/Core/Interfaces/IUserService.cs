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

        // Contact management
        Task<IEnumerable<UserDto>> FindUsersByPhoneNumbersAsync(IEnumerable<string> phoneNumbers, long currentUserId);
        Task<IEnumerable<UserDto>> GetSavedContactsAsync(long ownerUserId);
        Task<IEnumerable<UserDto>> GetSavedContactsWithChatInfoAsync(long ownerUserId);
        Task<bool> MarkContactAsFavoriteAsync(long ownerId, long contactId, bool isFavorite);
        Task<bool> BlockContactAsync(long ownerId, long contactId, bool isBlocked);

        // User status and activity
        Task<bool> UpdateUserLastActiveAsync(long userId);
        Task<bool> UpdateFcmTokenAsync(long userId, string fcmToken);
    }
}
