using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;
using System.Linq.Expressions;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class MessageRepository : Repository<Message>, IMessageRepository
    {
        private readonly new ChatDbContext _context; // ✅ اصلاح warning CS0108

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

        // ✅ متدهای مفقود از IMessageRepository اضافه شدن
        public async Task<Message?> GetByIdWithDetailsAsync(int messageId)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .Include(m => m.Chat)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task MarkAsReadAsync(int messageId, long readerId)
        {
            var message = await _context.Messages
                .Include(m => m.ReadStatuses)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.SenderId == readerId)
                return;

            var existingReadStatus = message.ReadStatuses
                .FirstOrDefault(rs => rs.ReaderId == readerId);

            if (existingReadStatus == null)
            {
                message.ReadStatuses.Add(new MessageReadStatus
                {
                    MessageId = messageId,
                    ReaderId = readerId,
                    ReadAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkMultipleAsReadAsync(List<int> messageIds, long readerId)
        {
            var messages = await _context.Messages
                .Include(m => m.ReadStatuses)
                .Where(m => messageIds.Contains(m.Id) && m.SenderId != readerId)
                .ToListAsync();

            foreach (var message in messages)
            {
                var existingReadStatus = message.ReadStatuses
                    .FirstOrDefault(rs => rs.ReaderId == readerId);

                if (existingReadStatus == null)
                {
                    message.ReadStatuses.Add(new MessageReadStatus
                    {
                        MessageId = message.Id,
                        ReaderId = readerId,
                        ReadAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(Guid chatId, long userId)
        {
            return await _context.Messages
                .CountAsync(m => m.ChatId == chatId &&
                               m.SenderId != userId &&
                               !m.ReadStatuses.Any(rs => rs.ReaderId == userId));
        }

        public async Task DeleteAllMessagesAsync(Guid chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .ToListAsync();

            _context.Messages.RemoveRange(messages);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Message>> GetMessagesAfterAsync(Guid chatId, DateTime afterTime, int take = 50)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .Where(m => m.ChatId == chatId && m.SentAt > afterTime)
                .OrderBy(m => m.SentAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<Message>> GetUnreadMessagesAsync(Guid chatId, long userId)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.ReadStatuses)
                    .ThenInclude(rs => rs.Reader)
                .Where(m => m.ChatId == chatId &&
                           m.SenderId != userId &&
                           !m.ReadStatuses.Any(rs => rs.ReaderId == userId))
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadMessagesCountAsync(Guid chatId, long userId)
        {
            return await _context.Messages
                .CountAsync(m => m.ChatId == chatId &&
                               m.SenderId != userId &&
                               !m.ReadStatuses.Any(rs => rs.ReaderId == userId));
        }

        public async Task<Message?> GetLastMessageAsync(Guid chatId)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Message>> SearchMessagesAsync(Guid chatId, string searchTerm, int skip = 0, int take = 20)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.ChatId == chatId &&
                           m.Content.Contains(searchTerm))
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> MarkMessageAsReadAsync(int messageId, long readerId)
        {
            var message = await _context.Messages
                .Include(m => m.ReadStatuses)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.SenderId == readerId)
                return false;

            var existingReadStatus = message.ReadStatuses
                .FirstOrDefault(rs => rs.ReaderId == readerId);

            if (existingReadStatus == null)
            {
                message.ReadStatuses.Add(new MessageReadStatus
                {
                    MessageId = messageId,
                    ReaderId = readerId,
                    ReadAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteMessageAsync(int messageId, long userId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null || message.SenderId != userId)
                return false;

            message.IsDeleted = true;
            message.Content = "[پیام حذف شده]";
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EditMessageAsync(int messageId, string newContent, long userId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null || message.SenderId != userId)
                return false;

            message.Content = newContent;
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Message>> GetUserMessagesAsync(long userId, int skip = 0, int take = 50)
        {
            return await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Chat)
                .Where(m => m.SenderId == userId)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
    }
}