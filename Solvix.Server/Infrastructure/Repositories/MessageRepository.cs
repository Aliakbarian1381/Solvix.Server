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

        // Override base methods for proper async handling
        public override async Task<Message> AddAsync(Message entity)
        {
            await _context.Messages.AddAsync(entity);
            return entity;
        }

        public override async Task<Message?> FindAsync(Expression<Func<Message, bool>> predicate)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .FirstOrDefaultAsync(predicate);
        }

        public override async Task<IReadOnlyList<Message>> ListAllAsync()
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .ToListAsync();
        }

        public override async Task<IReadOnlyList<Message>> ListAsync(
            Expression<Func<Message, bool>>? predicate = null,
            string? includeProperties = null)
        {
            IQueryable<Message> query = _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader);

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

        public override async Task<Message?> GetByIdAsync(object id)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .FirstOrDefaultAsync(m => m.Id.Equals(id));
        }

        // IMessageRepository specific implementations
        public async Task<List<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
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
                .Include(m => m.Chat)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task MarkAsReadAsync(int messageId, long readerId)
        {
            // Check if message exists and is not sent by the reader
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId != readerId);

            if (message == null)
                return;

            // Check if already marked as read
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
            // Get messages that are not sent by the reader and not already read
            var messagesToMark = await _context.Messages
                .Where(m => messageIds.Contains(m.Id) && m.SenderId != readerId)
                .Where(m => !m.ReadStatuses.Any(rs => rs.ReaderId == readerId))
                .Select(m => m.Id)
                .ToListAsync();

            if (messagesToMark.Count == 0)
                return;

            var readStatuses = messagesToMark.Select(messageId => new MessageReadStatus
            {
                MessageId = messageId,
                ReaderId = readerId,
                ReadAt = DateTime.UtcNow
            }).ToList();

            await _context.MessageReadStatuses.AddRangeAsync(readStatuses);
        }

        public async Task<int> GetUnreadCountAsync(Guid chatId, long userId)
        {
            return await _context.Messages
                .Where(m => m.ChatId == chatId && m.SenderId != userId && !m.IsDeleted)
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