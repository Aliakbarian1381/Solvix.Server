using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class ChatRepository : Repository<Chat>, IChatRepository
    {
        private readonly ChatDbContext _chatDbContext;

        public ChatRepository(ChatDbContext chatDbContext) : base(chatDbContext)
        {
            _chatDbContext = chatDbContext;
        }

        public async Task<List<Chat>> GetUserChatsAsync(long userId)
        {
            return await _chatDbContext.Chats
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                 .Include(c => c.Messages)
                .Where(c => c.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(c => c.Messages.Any(m => !m.IsDeleted) ? c.Messages.Where(m => !m.IsDeleted).Max(m => m.SentAt) : c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Chat?> GetChatWithParticipantsAsync(Guid chatId)
        {
            return await _chatDbContext.Chats
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);
        }


        public async Task<List<Chat>> SearchUserChatsAsync(long userId, string searchTerm)
        {
            var lowerSearchTerm = searchTerm.ToLower();

            return await _chatDbContext.Chats
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages)
                .Where(c => c.Participants.Any(p => p.UserId == userId)) // Only user's chats
                .Where(c =>
                    (c.IsGroup && c.Title != null && c.Title.ToLower().Contains(lowerSearchTerm)) ||
                    (!c.IsGroup && c.Participants.Any(p =>
                        p.UserId != userId &&
                        ((p.User.FirstName + " " + p.User.LastName).ToLower().Contains(lowerSearchTerm) ||
                         (p.User.UserName != null && p.User.UserName.ToLower().Contains(lowerSearchTerm)))
                    ))
                )
                .OrderByDescending(c => c.Messages.Any() ? c.Messages.Max(m => m.SentAt) : c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Chat?> GetPrivateChatBetweenUsersAsync(long user1Id, long user2Id)
        {
            var chats = await _chatDbContext.ChatParticipants
                .Where(cp => cp.UserId == user1Id || cp.UserId == user2Id)
                .GroupBy(cp => cp.ChatId)
                .Where(g => g.Count() == 2 && g.Select(cp => cp.UserId).Contains(user1Id) && g.Select(cp => cp.UserId).Contains(user2Id))
                .Select(g => g.Key)
                .ToListAsync();

            if (!chats.Any())
                return null;

            return await _chatDbContext.Chats
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == chats.First() && !c.IsGroup);
        }

        public async Task<bool> IsUserParticipantAsync(Guid chatId, long userId)
        {
            return await _chatDbContext.ChatParticipants
                .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);
        }

        public async Task AddParticipantAsync(Guid chatId, long userId)
        {
            var existingParticipant = await _chatDbContext.ChatParticipants
                .FirstOrDefaultAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

            if (existingParticipant == null)
            {
                await _chatDbContext.ChatParticipants.AddAsync(new ChatParticipant
                {
                    ChatId = chatId,
                    UserId = userId
                });
            }
        }

        public async Task RemoveParticipantAsync(Guid chatId, long userId)
        {
            var participant = await _chatDbContext.ChatParticipants
                .FirstOrDefaultAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

            if (participant != null)
            {
                _chatDbContext.ChatParticipants.Remove(participant);
            }
        }
    }
}
