using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using Solvix.Server.Models;

namespace Solvix.Server.Services
{
    public class MessageService : IMessageService
    {
        private readonly ChatDbContext _context;

        public MessageService(ChatDbContext context)
        {
            _context = context;
        }

        public async Task<Message> SaveMessage(long senderId, long recipientId, string content)
        {
            var message = new Message
            {
                SenderId = senderId,
                RecipientId = recipientId,
                Content = content,
                SentAt = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<Message> GetMessageById(int id)
        {
            return await _context.Messages.FindAsync(id);
        }


        public async Task<List<Message>> GetChatHistory(long userId, long otherUserId)   // تغییر به string
        {
            return await _context.Messages
                .Where(m => (m.SenderId == userId && m.RecipientId == otherUserId) || (m.SenderId == otherUserId && m.RecipientId == userId))
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }



        public async Task<List<Message>> GetUnreadMessagesForUser(long userId)
        {
            return await _context.Messages
                .Where(m => m.RecipientId == userId && m.ReadAt == null)
                .Include(m => m.Sender)
                .ToListAsync();
        }

        public async Task MarkMessagesAsRead(long senderId, long recipientId)
        {
            var unreadMessages = await _context.Messages
                .Where(m => m.SenderId == senderId && m.RecipientId == recipientId && m.ReadAt == null)
                .ToListAsync();
            foreach (var message in unreadMessages)
            {
                message.ReadAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }
    }
}
