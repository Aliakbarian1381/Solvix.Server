using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IChatRepository : IRepository<Chat>
    {
        Task<List<Chat>> GetUserChatsAsync(long userId);
        Task<Chat?> GetChatWithParticipantsAsync(Guid chatId);
        Task<Chat?> GetPrivateChatBetweenUsersAsync(long user1Id, long user2Id);
        Task<bool> IsUserParticipantAsync(Guid chatId, long userId);
        Task AddParticipantAsync(Guid chatId, long userId);
        Task RemoveParticipantAsync(Guid chatId, long userId);
        Task<List<Chat>> SearchUserChatsAsync(long userId, string searchTerm);
        Task<Participant?> GetParticipantAsync(Guid chatId, long userId);
        Task<int> GetParticipantCountAsync(Guid chatId);
        Task<List<Participant>> GetActiveParticipantsAsync(Guid chatId);
        Task<bool> IsUserOwnerAsync(Guid chatId, long userId);
        Task<bool> IsUserAdminAsync(Guid chatId, long userId);
        Task UpdateParticipantRoleAsync(Guid chatId, long userId, string newRole);
        Task<List<Chat>> GetPublicGroupsAsync(int skip = 0, int take = 20);
        Task<Chat?> GetChatByJoinLinkAsync(string joinLink);
    }
}