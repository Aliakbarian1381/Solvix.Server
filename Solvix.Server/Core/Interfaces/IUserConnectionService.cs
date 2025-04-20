using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IUserConnectionService
    {
        Task AddConnectionAsync(long userId, string connectionId);
        Task RemoveConnectionAsync(string connectionId);
        Task<List<string>> GetConnectionsForUserAsync(long userId);
        Task<long?> GetUserIdForConnectionAsync(string connectionId);
        Task<List<AppUser>> GetOnlineUsersAsync();
        Task<bool> IsUserOnlineAsync(long userId);
    }
}
