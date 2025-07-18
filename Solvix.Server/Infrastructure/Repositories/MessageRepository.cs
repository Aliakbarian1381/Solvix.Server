using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class MessageRepository : Repository<Message>, IMessageRepository
    {
        private readonly ChatDbContext _chatDbContext;

        public MessageRepository(ChatDbContext chatDbContext) : base(chatDbContext)
        {
            _chatDbContext = chatDbContext;
        }

        public async Task<List<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        {
            return await _chatDbContext.Messages
                .Include(m => m.Sender) 
                .Where(m => m.ChatId == chatId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt) 
                .Skip(skip)
                .Take(take)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadMessageCountAsync(Guid chatId, long userId)
        {
            return await _chatDbContext.Messages
                .CountAsync(m => m.ChatId == chatId && m.SenderId != userId && !m.IsRead);
        }

        public async Task<List<Message>> GetUnreadMessagesAsync(Guid chatId, long userId)
        {
            return await _chatDbContext.Messages
                .Where(m => m.ChatId == chatId && m.SenderId != userId && !m.IsRead)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int messageId, long userId)
        {
            var message = await _chatDbContext.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.IsRead || message.SenderId == userId)
                return;

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;

        }

        public async Task MarkMultipleAsReadAsync(List<int> messageIds, long userId)
        {
            if (messageIds == null || !messageIds.Any())
                return;

            var messagesToUpdate = await _chatDbContext.Messages
                .Where(m => messageIds.Contains(m.Id) && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            if (!messagesToUpdate.Any()) return;


            var now = DateTime.UtcNow;
            foreach (var message in messagesToUpdate)
            {
                message.IsRead = true;
                message.ReadAt = now;
            }

        }

        public async Task DeleteAllMessagesAsync(Guid chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .ToListAsync();
            _context.Messages.RemoveRange(messages);
        }

        public async Task MarkMultipleAsReadAsync(List<int> messageIds, long userId)
        {
            var messages = await _context.Messages
                .Where(m => messageIds.Contains(m.Id) && m.SenderId != userId)
                .ToListAsync();

            foreach (var message in messages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            _context.Messages.UpdateRange(messages);
        }
    }
}
