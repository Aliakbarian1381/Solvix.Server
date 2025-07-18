using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatDto>> GetUserChatsAsync(long userId);
        Task<ChatDto?> GetChatByIdAsync(Guid chatId, long userId);
        Task<(Guid chatId, bool alreadyExists)> StartChatWithUserAsync(long initiatorUserId, long recipientUserId);
        Task<Message> SaveMessageAsync(Guid chatId, long senderId, string content);
        Task BroadcastMessageAsync(Message message);
        Task<List<MessageDto>> GetChatMessagesAsync(Guid chatId, long userId, int skip = 0, int take = 50);
        Task MarkMessageAsReadAsync(int messageId, long readerUserId);
        Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId);
        Task<bool> IsUserParticipantAsync(Guid chatId, long userId);
        Task<Message?> EditMessageAsync(int messageId, string newContent, long editorUserId);
        Task<Message?> DeleteMessageAsync(int messageId, long deleterUserId);
        Task BroadcastMessageUpdateAsync(Message message);
        Task<ChatDto> CreateGroupChatAsync(long creatorId, string title, List<long> participantIds);
        Task<bool> HasAddMemberPermissionAsync(Guid chatId, long userId);
        Task<bool> HasRemoveMemberPermissionAsync(Guid chatId, long userId);
        Task<bool> HasChangeRolePermissionAsync(Guid chatId, long userId);
        Task<bool> IsGroupOwnerAsync(Guid chatId, long userId);
        Task AddMemberToGroupAsync(Guid chatId, long memberId);
        Task RemoveMemberFromGroupAsync(Guid chatId, long memberId);
        Task ChangeMemberRoleAsync(Guid chatId, long memberId, string newRole);
        Task LeaveGroupAsync(Guid chatId, long userId);
        Task DeleteGroupAsync(Guid chatId);
        Task<GroupInfoDto> GetGroupInfoAsync(Guid chatId, long userId);
        Task<GroupSettingsDto> GetGroupSettingsAsync(Guid chatId, long userId);
        Task UpdateGroupSettingsAsync(Guid chatId, long userId, GroupSettingsDto settings);

    }
}
