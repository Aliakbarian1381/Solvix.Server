using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;

namespace Solvix.Server.Infrastructure.Repositories
{
    public class ChatRepository : Repository<Chat>, IChatRepository
    {
        private readonly new ChatDbContext _context;

        public ChatRepository(ChatDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Chat>> GetUserChatsAsync(long userId)
        {
            return await _context.Chats
                .Include(c => c.Participants.Where(p => p.IsActive))
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                    .ThenInclude(m => m.Sender)
                .Where(c => c.Participants.Any(p => p.UserId == userId && p.IsActive))
                .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Chat?> GetChatWithParticipantsAsync(Guid chatId)
        {
            return await _context.Chats
                .Include(c => c.Participants.Where(p => p.IsActive))
                    .ThenInclude(p => p.User)
                .Include(c => c.GroupMembers)
                    .ThenInclude(gm => gm.User)
                .Include(c => c.GroupSettings)
                .FirstOrDefaultAsync(c => c.Id == chatId);
        }

        public async Task<Chat?> GetPrivateChatBetweenUsersAsync(long user1Id, long user2Id)
        {
            return await _context.Chats
                .Include(c => c.Participants.Where(p => p.IsActive))
                    .ThenInclude(p => p.User)
                .Where(c => !c.IsGroup &&
                           c.Participants.Count(p => p.IsActive) == 2 &&
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
                // Reactivate if exists but inactive
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
                // Soft delete - mark as inactive
                participant.IsActive = false;
            }
        }

        public async Task<List<Chat>> SearchUserChatsAsync(long userId, string searchTerm)
        {
            var normalizedSearchTerm = searchTerm.ToLower();

            return await _context.Chats
                .Include(c => c.Participants.Where(p => p.IsActive))
                    .ThenInclude(p => p.User)
                .Where(c => c.Participants.Any(p => p.UserId == userId && p.IsActive) &&
                           (c.Title != null && c.Title.ToLower().Contains(normalizedSearchTerm) ||
                            c.Participants.Any(p => p.IsActive &&
                                (p.User.UserName != null && p.User.UserName.ToLower().Contains(normalizedSearchTerm) ||
                                 p.User.FirstName != null && p.User.FirstName.ToLower().Contains(normalizedSearchTerm) ||
                                 p.User.LastName != null && p.User.LastName.ToLower().Contains(normalizedSearchTerm)))))
                .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Participant?> GetParticipantAsync(Guid chatId, long userId)
        {
            return await _context.Participants
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId && p.IsActive);
        }

        public async Task<int> GetParticipantCountAsync(Guid chatId)
        {
            return await _context.Participants
                .CountAsync(p => p.ChatId == chatId && p.IsActive);
        }

        public async Task<List<Participant>> GetActiveParticipantsAsync(Guid chatId)
        {
            return await _context.Participants
                .Include(p => p.User)
                .Where(p => p.ChatId == chatId && p.IsActive)
                .ToListAsync();
        }

        public async Task<bool> IsUserOwnerAsync(Guid chatId, long userId)
        {
            var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            return chat?.OwnerId == userId;
        }

        public async Task<bool> IsUserAdminAsync(Guid chatId, long userId)
        {
            var participant = await GetParticipantAsync(chatId, userId);
            return participant?.Role == "Admin" || await IsUserOwnerAsync(chatId, userId);
        }

        public async Task UpdateParticipantRoleAsync(Guid chatId, long userId, string newRole)
        {
            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId && p.IsActive);

            if (participant != null)
            {
                participant.Role = newRole;
            }
        }

        public async Task<List<Chat>> GetPublicGroupsAsync(int skip = 0, int take = 20)
        {
            return await _context.Chats
                .Include(c => c.Participants.Where(p => p.IsActive))
                    .ThenInclude(p => p.User)
                .Where(c => c.IsGroup && c.IsPublic)
                .OrderByDescending(c => c.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<Chat?> GetChatByJoinLinkAsync(string joinLink)
        {
            return await _context.Chats
                .Include(c => c.Participants.Where(p => p.IsActive))
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.IsGroup && c.JoinLink == joinLink);
        }
    }
}