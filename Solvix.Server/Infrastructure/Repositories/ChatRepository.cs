using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class ChatRepository : Repository<Chat>, IChatRepository
    {
        private readonly ChatDbContext _context;

        public ChatRepository(ChatDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Chat>> GetUserChatsAsync(long userId)
        {
            return await _context.Chats
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .Where(c => c.Participants.Any(p => p.UserId == userId && p.IsActive))
                .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Chat?> GetChatWithParticipantsAsync(Guid chatId)
        {
            return await _context.Chats
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.GroupMembers)
                    .ThenInclude(gm => gm.User)
                .Include(c => c.GroupSettings)
                .FirstOrDefaultAsync(c => c.Id == chatId);
        }

        public async Task<Chat?> GetPrivateChatBetweenUsersAsync(long user1Id, long user2Id)
        {
            return await _context.Chats
                .Include(c => c.Participants)
                .Where(c => !c.IsGroup &&
                           c.Participants.Count == 2 &&
                           c.Participants.Any(p => p.UserId == user1Id && p.IsActive) &&
                           c.Participants.Any(p => p.UserId == user2Id && p.IsActive))
                .FirstOrDefaultAsync();
        }

        public async Task<bool> IsUserParticipantAsync(Guid chatId, long userId)
        {
            return await _context.Participants
                .AnyAsync(p => p.ChatId == chatId && p.UserId == userId && p.IsActive);
        }

        public async Task AddParticipantAsync(Guid chatId, long userId)
        {
            var existingParticipant = await _context.Participants
                .FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId);

            if (existingParticipant != null)
            {
                existingParticipant.IsActive = true;
                existingParticipant.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                var participant = new Participant
                {
                    ChatId = chatId,
                    UserId = userId,
                    Role = "Member",
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _context.Participants.AddAsync(participant);
            }
        }

        public async Task RemoveParticipantAsync(Guid chatId, long userId)
        {
            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId);

            if (participant != null)
            {
                participant.IsActive = false;
            }
        }

        public async Task<List<Chat>> SearchUserChatsAsync(long userId, string searchTerm)
        {
            return await _context.Chats
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Where(c => c.Participants.Any(p => p.UserId == userId && p.IsActive) &&
                           (c.Title != null && c.Title.Contains(searchTerm) ||
                            c.Participants.Any(p => p.User.UserName != null && p.User.UserName.Contains(searchTerm) ||
                                                   p.User.FirstName != null && p.User.FirstName.Contains(searchTerm) ||
                                                   p.User.LastName != null && p.User.LastName.Contains(searchTerm))))
                .ToListAsync();
        }

        public async Task<Participant?> GetParticipantAsync(Guid chatId, long userId)
        {
            return await _context.Participants
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId && p.IsActive);
        }
    }
}