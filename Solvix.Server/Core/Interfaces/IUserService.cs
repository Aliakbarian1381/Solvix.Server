using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IUserService
    {
        Task<AppUser?> GetUserByUsernameAsync(string username);
        Task<AppUser?> GetUserByIdAsync(long userId);
        Task<IdentityResult> CreateUserAsync(AppUser user, string password);
        Task<bool> CheckPhoneExistsAsync(string phoneNumber);
        Task<List<UserDto>> SearchUsersAsync(string searchTerm, long currentUserId, int limit = 20);
        Task<bool> UpdateUserLastActiveAsync(long userId);
        Task<bool> UpdateFcmTokenAsync(long userId, string fcmToken);
    }
}
