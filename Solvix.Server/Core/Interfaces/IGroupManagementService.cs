using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IGroupManagementService
    {
        Task<GroupInfoDto?> GetGroupInfoAsync(Guid chatId, long requesterId);
        Task<bool> UpdateGroupInfoAsync(Guid chatId, long requesterId, UpdateGroupDto dto);
        Task<bool> UpdateGroupSettingsAsync(Guid chatId, long requesterId, GroupSettingsDto settings);
        Task<bool> AddMembersAsync(Guid chatId, long adminId, List<long> userIds);
        Task<bool> RemoveMemberAsync(Guid chatId, long adminId, long memberId);
        Task<bool> UpdateMemberRoleAsync(Guid chatId, long adminId, long memberId, GroupRole newRole);
        Task<bool> LeaveGroupAsync(Guid chatId, long userId);
        Task<bool> DeleteGroupAsync(Guid chatId, long ownerId);
        Task<bool> TransferOwnershipAsync(Guid chatId, long currentOwnerId, long newOwnerId);
        Task<List<GroupMemberDto>> GetGroupMembersAsync(Guid chatId, long requesterId);
    }
}

