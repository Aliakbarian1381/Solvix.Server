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
    }
}
