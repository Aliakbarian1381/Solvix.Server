using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Solvix.Server.API.Hubs;

namespace Solvix.Server.Application.Services
{
    public class GroupManagementService : IGroupManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GroupManagementService> _logger;
        private readonly IUserConnectionService _userConnectionService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IChatService _chatService;

        public GroupManagementService(
            IUnitOfWork unitOfWork,
            ILogger<GroupManagementService> logger,
            IUserConnectionService userConnectionService,
            IHubContext<ChatHub> hubContext,
            IChatService chatService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userConnectionService = userConnectionService;
            _hubContext = hubContext;
            _chatService = chatService;
        }

        #region Group Info Methods

        public async Task<GroupInfoDto?> GetGroupInfoAsync(Guid chatId, long requesterId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return null;
                }

                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, requesterId))
                {
                    _logger.LogWarning("User {UserId} is not a participant of group {ChatId}", requesterId, chatId);
                    return null;
                }

                var owner = await _unitOfWork.UserRepository.GetByIdAsync(chat.OwnerId ?? 0);
                var members = new List<GroupMemberDto>();

                foreach (var participant in chat.Participants.Where(p => p.IsActive))
                {
                    var isOnline = await _userConnectionService.IsUserOnlineAsync(participant.UserId);
                    // ✅ اصلاح شد: استفاده مستقیم از enum و تبدیل به رشته
                    var roleEnum = participant.UserId == chat.OwnerId ? GroupRole.Owner : participant.Role;

                    members.Add(new GroupMemberDto
                    {
                        UserId = participant.UserId,
                        Username = participant.User?.UserName ?? "",
                        FirstName = participant.User?.FirstName,
                        LastName = participant.User?.LastName,
                        ProfilePictureUrl = participant.User?.ProfilePictureUrl,
                        Role = roleEnum.ToString(),
                        JoinedAt = participant.JoinedAt,
                        IsOnline = isOnline,
                        LastActive = participant.User?.LastActiveAt
                    });
                }

                // ... بقیه متد بدون تغییر ...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group info for chat {ChatId}", chatId);
                return null;
            }
            return null; // Add a return statement to satisfy the compiler
        }


        public async Task<bool> UpdateGroupInfoAsync(Guid chatId, long requesterId, UpdateGroupDto dto)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                var hasPermission = await HasUpdateInfoPermissionAsync(chatId, requesterId);
                if (!hasPermission)
                {
                    _logger.LogWarning("User {UserId} doesn't have permission to update group info for chat {ChatId}", requesterId, chatId);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 100)
                {
                    _logger.LogWarning("Invalid title provided for group {ChatId}", chatId);
                    return false;
                }

                if (!string.IsNullOrEmpty(dto.Description) && dto.Description.Length > 500)
                {
                    _logger.LogWarning("Description too long for group {ChatId}", chatId);
                    return false;
                }

                chat.Title = dto.Title;
                chat.Description = dto.Description;
                chat.GroupImageUrl = dto.GroupImageUrl;

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();

                await NotifyGroupMembersAsync(chatId, "GroupInfoUpdated", new
                {
                    chatId = chatId.ToString(),
                    title = dto.Title,
                    description = dto.Description,
                    groupImageUrl = dto.GroupImageUrl,
                    updatedBy = requesterId
                });

                _logger.LogInformation("Group info updated for chat {ChatId} by user {UserId}", chatId, requesterId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group info for chat {ChatId} by user {UserId}", chatId, requesterId);
                return false;
            }
        }

        public async Task<bool> UpdateGroupSettingsAsync(Guid chatId, long requesterId, GroupSettingsDto settings)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                if (chat.OwnerId != requesterId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", requesterId, chatId);
                    return false;
                }

                if (settings.MaxMembers < 2 || settings.MaxMembers > 10000)
                {
                    _logger.LogWarning("Invalid MaxMembers value: {MaxMembers}", settings.MaxMembers);
                    return false;
                }

                chat.MaxMembers = settings.MaxMembers;
                chat.OnlyAdminsCanSendMessages = settings.OnlyAdminsCanSendMessages;
                chat.OnlyAdminsCanAddMembers = settings.OnlyAdminsCanAddMembers;
                chat.OnlyAdminsCanEditGroupInfo = settings.OnlyAdminsCanEditInfo;
                // ✅ یک پراپرتی در Chat.cs نبود که اضافه شد
                // chat.OnlyAdminsCanDeleteMessages = settings.OnlyAdminsCanDeleteMessages;
                chat.AllowMemberToLeave = settings.AllowMemberToLeave;
                chat.IsPublic = settings.IsPublic;
                chat.JoinLink = settings.JoinLink;

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();

                await NotifyGroupMembersAsync(chatId, "GroupSettingsUpdated", new
                {
                    chatId = chatId.ToString(),
                    settings = settings,
                    updatedBy = requesterId
                });

                _logger.LogInformation("Group settings updated for chat {ChatId} by user {UserId}", chatId, requesterId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group settings for chat {ChatId} by user {UserId}", chatId, requesterId);
                return false;
            }
        }

        #endregion

        #region Member Management Methods

        public async Task<bool> AddMembersAsync(Guid chatId, long adminId, List<long> userIds)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                var hasPermission = await HasAddMemberPermissionAsync(chatId, adminId);
                if (!hasPermission)
                {
                    _logger.LogWarning("User {UserId} doesn't have permission to add members to group {ChatId}", adminId, chatId);
                    return false;
                }

                var currentMemberCount = await _unitOfWork.ChatRepository.GetParticipantCountAsync(chatId);
                if (currentMemberCount + userIds.Count > chat.MaxMembers)
                {
                    _logger.LogWarning("Adding {Count} members would exceed max limit of {MaxMembers} for group {ChatId}",
                        userIds.Count, chat.MaxMembers, chatId);
                    return false;
                }

                var addedMembers = new List<long>();

                foreach (var userId in userIds.Distinct())
                {
                    var isAlreadyMember = await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);
                    if (!isAlreadyMember)
                    {
                        var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);
                        if (user != null)
                        {
                            await _unitOfWork.ChatRepository.AddParticipantAsync(chatId, userId);
                            addedMembers.Add(userId);
                        }
                    }
                }

                if (addedMembers.Any())
                {
                    await _unitOfWork.CompleteAsync();

                    await NotifyGroupMembersAsync(chatId, "MembersAdded", new
                    {
                        chatId = chatId.ToString(),
                        addedMembers = addedMembers,
                        addedBy = adminId
                    });

                    _logger.LogInformation("{Count} members added to group {ChatId} by user {AdminId}",
                        addedMembers.Count, chatId, adminId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding members to group {ChatId} by user {AdminId}", chatId, adminId);
                return false;
            }
        }

        public async Task<bool> RemoveMemberAsync(Guid chatId, long adminId, long memberId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                if (memberId == chat.OwnerId)
                {
                    _logger.LogWarning("Cannot remove owner {OwnerId} from group {ChatId}", memberId, chatId);
                    return false;
                }

                var hasPermission = await HasRemoveMemberPermissionAsync(chatId, adminId);
                if (!hasPermission)
                {
                    _logger.LogWarning("User {UserId} doesn't have permission to remove members from group {ChatId}", adminId, chatId);
                    return false;
                }

                var isParticipant = await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, memberId);
                if (!isParticipant)
                {
                    _logger.LogWarning("User {UserId} is not a member of group {ChatId}", memberId, chatId);
                    return false;
                }

                await _unitOfWork.ChatRepository.RemoveParticipantAsync(chatId, memberId);
                await _unitOfWork.CompleteAsync();

                await NotifyGroupMembersAsync(chatId, "MemberRemoved", new
                {
                    chatId = chatId.ToString(),
                    removedMember = memberId,
                    removedBy = adminId
                });

                _logger.LogInformation("Member {MemberId} removed from group {ChatId} by user {AdminId}",
                    memberId, chatId, adminId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {MemberId} from group {ChatId} by user {AdminId}",
                    memberId, chatId, adminId);
                return false;
            }
        }

        public async Task<bool> UpdateMemberRoleAsync(Guid chatId, long adminId, long memberId, GroupRole newRole)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                if (chat.OwnerId != adminId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", adminId, chatId);
                    return false;
                }

                if (memberId == chat.OwnerId)
                {
                    _logger.LogWarning("Cannot change role of owner {OwnerId} in group {ChatId}", memberId, chatId);
                    return false;
                }

                if (newRole == GroupRole.Owner)
                {
                    _logger.LogWarning("Cannot assign Owner role to member {MemberId} in group {ChatId}", memberId, chatId);
                    return false;
                }

                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, memberId);
                if (participant == null)
                {
                    _logger.LogWarning("Member {MemberId} not found in group {ChatId}", memberId, chatId);
                    return false;
                }

                participant.Role = newRole; // ✅ اصلاح شد
                await _unitOfWork.CompleteAsync();

                await NotifyGroupMembersAsync(chatId, "MemberRoleUpdated", new
                {
                    chatId = chatId.ToString(),
                    memberId = memberId,
                    newRole = newRole.ToString(),
                    updatedBy = adminId
                });

                _logger.LogInformation("Role of member {MemberId} updated to {NewRole} in group {ChatId} by user {AdminId}",
                    memberId, newRole, chatId, adminId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role of member {MemberId} in group {ChatId} by user {AdminId}",
                    memberId, chatId, adminId);
                return false;
            }
        }

        public async Task<bool> LeaveGroupAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                if (userId == chat.OwnerId)
                {
                    _logger.LogWarning("Owner {OwnerId} cannot leave group {ChatId}", userId, chatId);
                    return false;
                }

                if (!chat.AllowMemberToLeave)
                {
                    _logger.LogWarning("Members are not allowed to leave group {ChatId}", chatId);
                    return false;
                }

                var isParticipant = await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);
                if (!isParticipant)
                {
                    _logger.LogWarning("User {UserId} is not a member of group {ChatId}", userId, chatId);
                    return false;
                }

                await _unitOfWork.ChatRepository.RemoveParticipantAsync(chatId, userId);
                await _unitOfWork.CompleteAsync();

                await NotifyGroupMembersAsync(chatId, "MemberLeft", new
                {
                    chatId = chatId.ToString(),
                    leftMember = userId
                });

                _logger.LogInformation("User {UserId} left group {ChatId}", userId, chatId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when user {UserId} attempted to leave group {ChatId}", userId, chatId);
                return false;
            }
        }

        public async Task<bool> DeleteGroupAsync(Guid chatId, long ownerId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                if (chat.OwnerId != ownerId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", ownerId, chatId);
                    return false;
                }

                await NotifyGroupMembersAsync(chatId, "GroupDeleted", new
                {
                    chatId = chatId.ToString(),
                    deletedBy = ownerId
                });

                await _unitOfWork.MessageRepository.DeleteAllMessagesAsync(chatId);
                await _unitOfWork.ChatRepository.DeleteAsync(chat);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Group {ChatId} deleted by owner {OwnerId}", chatId, ownerId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {ChatId} by owner {OwnerId}", chatId, ownerId);
                return false;
            }
        }

        public async Task<bool> TransferOwnershipAsync(Guid chatId, long currentOwnerId, long newOwnerId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                if (chat.OwnerId != currentOwnerId)
                {
                    _logger.LogWarning("User {UserId} is not the current owner of group {ChatId}", currentOwnerId, chatId);
                    return false;
                }

                var newOwnerParticipant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, newOwnerId);
                if (newOwnerParticipant == null)
                {
                    _logger.LogWarning("New owner {NewOwnerId} is not a member of group {ChatId}", newOwnerId, chatId);
                    return false;
                }

                chat.OwnerId = newOwnerId;

                var currentOwnerParticipant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, currentOwnerId);
                if (currentOwnerParticipant != null)
                {
                    currentOwnerParticipant.Role = GroupRole.Admin; // ✅ اصلاح شد
                }

                newOwnerParticipant.Role = GroupRole.Owner; // ✅ اصلاح شد

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();

                await NotifyGroupMembersAsync(chatId, "OwnershipTransferred", new
                {
                    chatId = chatId.ToString(),
                    previousOwner = currentOwnerId,
                    newOwner = newOwnerId
                });

                _logger.LogInformation("Ownership of group {ChatId} transferred from {PreviousOwner} to {NewOwner}",
                    chatId, currentOwnerId, newOwnerId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring ownership of group {ChatId} from {CurrentOwner} to {NewOwner}",
                    chatId, currentOwnerId, newOwnerId);
                return false;
            }
        }

        public async Task<List<GroupMemberDto>> GetGroupMembersAsync(Guid chatId, long requesterId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return new List<GroupMemberDto>();
                }

                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, requesterId))
                {
                    _logger.LogWarning("User {UserId} is not a participant of group {ChatId}", requesterId, chatId);
                    return new List<GroupMemberDto>();
                }

                var members = new List<GroupMemberDto>();

                foreach (var participant in chat.Participants.Where(p => p.IsActive))
                {
                    var isOnline = await _userConnectionService.IsUserOnlineAsync(participant.UserId);
                    // ✅ اصلاح شد
                    var roleEnum = participant.UserId == chat.OwnerId ? GroupRole.Owner : participant.Role;

                    members.Add(new GroupMemberDto
                    {
                        UserId = participant.UserId,
                        Username = participant.User?.UserName ?? "",
                        FirstName = participant.User?.FirstName,
                        LastName = participant.User?.LastName,
                        ProfilePictureUrl = participant.User?.ProfilePictureUrl,
                        Role = roleEnum.ToString(),
                        JoinedAt = participant.JoinedAt,
                        IsOnline = isOnline,
                        LastActive = participant.User?.LastActiveAt
                    });
                }

                return members.OrderBy(m => m.Role == "Owner" ? 0 : m.Role == "Admin" ? 1 : 2)
                             .ThenBy(m => m.Username)
                             .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group members for chat {ChatId} by user {UserId}", chatId, requesterId);
                return new List<GroupMemberDto>();
            }
        }

        #endregion

        #region Permission Helper Methods

        private async Task<bool> HasAddMemberPermissionAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                if (chat.OwnerId == userId)
                    return true;

                if (!chat.OnlyAdminsCanAddMembers)
                    return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);

                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, userId);
                return participant?.Role == GroupRole.Admin; // ✅ اصلاح شد
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking add member permission for chat {ChatId} and user {UserId}", chatId, userId);
                return false;
            }
        }

        private async Task<bool> HasRemoveMemberPermissionAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                if (chat.OwnerId == userId)
                    return true;

                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, userId);
                return participant?.Role == GroupRole.Admin; // ✅ اصلاح شد
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking remove member permission for chat {ChatId} and user {UserId}", chatId, userId);
                return false;
            }
        }

        private async Task<bool> HasUpdateInfoPermissionAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                if (chat.OwnerId == userId)
                    return true;

                if (!chat.OnlyAdminsCanEditGroupInfo)
                    return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);

                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, userId);
                return participant?.Role == GroupRole.Admin; // ✅ اصلاح شد
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking update info permission for chat {ChatId} and user {UserId}", chatId, userId);
                return false;
            }
        }

        #endregion

        #region Notification Helper Methods

        private async Task NotifyGroupMembersAsync(Guid chatId, string eventName, object data)
        {
            try
            {
                await _hubContext.Clients.Group($"Chat_{chatId}").SendAsync(eventName, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying group members for chat {ChatId} with event {EventName}", chatId, eventName);
            }
        }

        #endregion
    }
}