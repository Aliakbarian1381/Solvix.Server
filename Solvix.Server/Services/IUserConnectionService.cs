using Solvix.Server.Models;
using System.Threading.Tasks;

namespace Solvix.Server.Services
{
    public interface IUserConnectionService
    {
        Task AddConnection(long userId, string connectionId);
        Task RemoveConnection(string connectionId);
        Task<List<string>> GetConnectionsForUser(long userId);
        Task<long?> GetUserIdForConnection(string connectionId);
        Task<List<AppUser>> GetOnlineUsers();

    }
}
