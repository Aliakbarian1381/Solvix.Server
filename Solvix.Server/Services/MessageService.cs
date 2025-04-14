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

        public async Task<Message> SaveMessage(int senderId, int recipientId, string content)
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

        public async Task<List<Message>> GetUnreadMessagesForUser(int userId)
        {
            return await _context.Messages
                .Where(m => m.RecipientId == userId && m.ReadAt == null)
                .Include(m => m.Sender)
                .ToListAsync();
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                message.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
