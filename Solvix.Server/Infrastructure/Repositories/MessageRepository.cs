using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;
using System.Linq.Expressions;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class MessageRepository : Repository<Message>, IMessageRepository
    {
        private readonly ChatDbContext _context;

        public MessageRepository(ChatDbContext context) : base(context)
        {
            _context = context;
        }

        // Override base methods to fix return types
        public override async Task<Message> AddAsync(Message entity)
        {
            await _context.Messages.AddAsync(entity);
            return entity;
        }

        public override async Task<Message?> FindAsync(Expression<Func<Message, bool>> predicate)
        {
            return await _context.Messages.FirstOrDefaultAsync(predicate);
        }

        public override async Task<IReadOnlyList<Message>> ListAllAsync()
        {
            return await _context.Messages.ToListAsync();
        }

        public override async Task<IReadOnlyList<Message>> ListAsync(Expression<Func<Message, bool>>? predicate = null, string? includeProperties = null)
        {
            IQueryable<Message> query = _context.Messages;

            if (predicate != null)
                query = query.Where(predicate);

            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var includeProperty in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty.Trim());
                }
            }

            return await query.ToListAsync();
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
            foreach (var messageId in messageIds)
            {
                await MarkAsReadAsync(messageId, readerId);
            }
        }

        public async Task<int> GetUnreadCountAsync(Guid chatId, long userId)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId && m.SenderId != userId)
                .Where(m => !m.ReadStatuses.Any(rs => rs.ReaderId == userId))
                .CountAsync();
        }

        public async Task DeleteAllMessagesAsync(Guid chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .ToListAsync();
            _context.Messages.RemoveRange(messages);
        }
    }
}