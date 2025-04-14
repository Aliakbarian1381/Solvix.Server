using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using Solvix.Server.Models;

namespace Solvix.Server.Services
{
    public class UserConnectionService : IUserConnectionService
    {
        private readonly ChatDbContext _context;

        public UserConnectionService(ChatDbContext context)
        {
            _context = context;
        }

        public async Task AddConnection(int userId, string connectionId)
        {
            var existingConnection = await _context.UserConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

            if (existingConnection != null)
            {
                existingConnection.UserId = userId;
                existingConnection.ConnectedAt = DateTime.UtcNow;
            }
            else
            {
                _context.UserConnections.Add(new UserConnection { UserId = userId, ConnectionId = connectionId });
            }
            await _context.SaveChangesAsync();
        }

        public async Task RemoveConnection(string connectionId)
        {
            var connection = await _context.UserConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
            if (connection != null)
            {
                _context.UserConnections.Remove(connection);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<string>> GetConnectionsForUser(int userId)
        {
            return await _context.UserConnections
                .Where(c => c.UserId == userId)
                .Select(c => c.ConnectionId)
                .ToListAsync();
        }

        public async Task<int> GetUserIdForConnection(string connectionId)
        {
            var connection = await _context.UserConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
            return connection?.UserId ?? 0;
        }

        public async Task<List<User>> GetOnlineUsers()
        {
            var onlineUserIds = await _context.UserConnections.Select(c => c.UserId).Distinct().ToListAsync();
            return await _context.Users.Where(u => onlineUserIds.Contains(u.Id)).ToListAsync();
        }

    }
}
