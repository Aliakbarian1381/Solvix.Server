using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Data;
using Solvix.Server.Core.Interfaces;


namespace Solvix.Server.Infrastructure.Services
{
    public class UserConnectionService : IUserConnectionService
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<UserConnectionService> _logger;

        public UserConnectionService(ChatDbContext context, ILogger<UserConnectionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task AddConnectionAsync(long userId, string connectionId)
        {
            try
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
                    _context.UserConnections.Add(new UserConnection
                    {
                        UserId = userId,
                        ConnectionId = connectionId,
                        ConnectedAt = DateTime.UtcNow
                    });
                }

                // بروزرسانی آخرین زمان فعالیت کاربر
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastActiveAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Added connection {ConnectionId} for user {UserId}", connectionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding connection {ConnectionId} for user {UserId}", connectionId, userId);
                throw;
            }
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            try
            {
                var connection = await _context.UserConnections
                    .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

                if (connection != null)
                {
                    // ذخیره آخرین زمان فعالیت کاربر قبل از حذف اتصال
                    var userId = connection.UserId;
                    var user = await _context.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.LastActiveAt = DateTime.UtcNow;
                    }

                    _context.UserConnections.Remove(connection);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Removed connection {ConnectionId} for user {UserId}", connectionId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing connection {ConnectionId}", connectionId);
                throw;
            }
        }

        public async Task<List<string>> GetConnectionsForUserAsync(long userId)
        {
            try
            {
                return await _context.UserConnections
                    .Where(c => c.UserId == userId)
                    .Select(c => c.ConnectionId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connections for user {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<long?> GetUserIdForConnectionAsync(string connectionId)
        {
            try
            {
                var connection = await _context.UserConnections
                    .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
                return connection?.UserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID for connection {ConnectionId}", connectionId);
                return null;
            }
        }

        public async Task<List<AppUser>> GetOnlineUsersAsync()
        {
            try
            {
                return await _context.Users
                    .Where(u => _context.UserConnections.Any(c => c.UserId == u.Id))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online users");
                return new List<AppUser>();
            }
        }

        public async Task<bool> IsUserOnlineAsync(long userId)
        {
            try
            {
                return await _context.UserConnections
                    .AnyAsync(c => c.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is online", userId);
                return false;
            }
        }
    }
}
