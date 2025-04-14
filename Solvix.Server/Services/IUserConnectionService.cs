using Solvix.Server.Models;


namespace Solvix.Server.Services
{
    public interface IUserConnectionService
    {
        Task AddConnection(int userId, string connectionId);
        Task RemoveConnection(string connectionId);
        Task<List<string>> GetConnectionsForUser(int userId);
        Task<int> GetUserIdForConnection(string connectionId);
        Task<List<User>> GetOnlineUsers();

    }
}
