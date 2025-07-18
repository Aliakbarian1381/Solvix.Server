using Google;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Infrastructure.Data;
using System.Linq.Expressions;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class MessageRepository : IRepository<Message>, IMessageRepository
    {
        private readonly ApplicationDbContext _context;

        public MessageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // IRepository<Message> implementations
        public async Task AddAsync(Message entity)
        {
            await _context.Messages.AddAsync(entity);
        }

        public async Task<bool> AnyAsync(Expression<Func<Message, bool>> predicate)
        {
            return await _context.Messages.AnyAsync(predicate);
        }

        public async Task<int> CountAsync(Expression<Func<Message, bool>> predicate)
        {
            return await _context.Messages.CountAsync(predicate);
        }

        public async Task DeleteAsync(Message entity)
        {
            _context.Messages.Remove(entity);
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<Message>> FindAsync(Expression<Func<Message, bool>> predicate)
        {
            return await _context.Messages.Where(predicate).ToListAsync();
        }

        public async Task<Message?> FirstOrDefaultAsync(Expression<Func<Message, bool>> predicate)
        {
            return await _context.Messages.FirstOrDefaultAsync(predicate);
        }

        public async Task<Message?> GetByIdAsync(object id)
        {
            return await _context.Messages.FindAsync(id);
        }

        public IQueryable<Message> GetQueryable()
        {
            return _context.Messages.AsQueryable();
        }

        public async Task<IEnumerable<Message>> ListAllAsync()
        {
            return await _context.Messages.ToListAsync();
        }

        public async Task<IEnumerable<Message>> ListAsync(Expression<Func<Message, bool>>? predicate = null, string? orderBy = null)
        {
            IQueryable<Message> query = _context.Messages;

            if (predicate != null)
                query = query.Where(predicate);

            if (!string.IsNullOrEmpty(orderBy))
            {
                // Simple ordering implementation
                if (orderBy.Contains("desc", StringComparison.OrdinalIgnoreCase))
                    query = query.OrderByDescending(m => m.SentAt);
                else
                    query = query.OrderBy(m => m.SentAt);
            }

            return await query.ToListAsync();
        }

        public async Task UpdateAsync(Message entity)
        {
            _context.Messages.Update(entity);
            await Task.CompletedTask;
        }

        // IMessageRepository specific implementations
        public async Task<List<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<Message?> GetByIdWithDetailsAsync(int messageId)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task MarkAsReadAsync(int messageId, long readerId)
        {
            var existingStatus = await _context.MessageReadStatuses
                .FirstOrDefaultAsync(mrs => mrs.MessageId == messageId && mrs.ReaderId == readerId);

            if (existingStatus == null)
            {
                var readStatus = new MessageReadStatus
                {
                    MessageId = messageId,
                    ReaderId = readerId,
                    ReadAt = DateTime.UtcNow
                };
                await _context.MessageReadStatuses.AddAsync(readStatus);
            }
        }

        public async Task MarkMultipleAsReadAsync(List<int> messageIds, long readerId)
        {
            var existingStatuses = await _context.MessageReadStatuses
                .Where(mrs => messageIds.Contains(mrs.MessageId) && mrs.ReaderId == readerId)
                .Select(mrs => mrs.MessageId)
                .ToListAsync();

            var newMessageIds = messageIds.Except(existingStatuses).ToList();

            var newStatuses = newMessageIds.Select(messageId => new MessageReadStatus
            {
                MessageId = messageId,
                ReaderId = readerId,
                ReadAt = DateTime.UtcNow
            }).ToList();

            if (newStatuses.Any())
            {
                await _context.MessageReadStatuses.AddRangeAsync(newStatuses);
            }
        }

        public async Task<int> GetUnreadCountAsync(Guid chatId, long userId)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId &&
                           m.SenderId != userId &&
                           !m.ReadStatuses.Any(rs => rs.ReaderId == userId))
                .CountAsync();
        }
    }
}