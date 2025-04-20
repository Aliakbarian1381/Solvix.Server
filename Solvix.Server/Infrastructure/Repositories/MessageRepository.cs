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
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .Skip(skip)
                .Take(take)
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
                .Include(m => m.Chat)
                .ThenInclude(c => c.Participants)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.SenderId == userId || message.IsRead)
                return;

            // بررسی آیا کاربر عضو چت است
            if (!message.Chat.Participants.Any(p => p.UserId == userId))
                return;

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
        }

        public async Task MarkMultipleAsReadAsync(List<int> messageIds, long userId)
        {
            var messages = await _chatDbContext.Messages
                .Include(m => m.Chat)
                .ThenInclude(c => c.Participants)
                .Where(m => messageIds.Contains(m.Id) && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            foreach (var message in messages)
            {
                // بررسی آیا کاربر عضو چت است
                if (message.Chat.Participants.Any(p => p.UserId == userId))
                {
                    message.IsRead = true;
                    message.ReadAt = DateTime.UtcNow;
                }
            }
        }
    }
}
