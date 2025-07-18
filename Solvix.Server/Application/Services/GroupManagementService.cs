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

        public GroupManagementService(
            IUnitOfWork unitOfWork,
            ILogger<GroupManagementService> logger,
            IUserConnectionService userConnectionService,
            IHubContext<ChatHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userConnectionService = userConnectionService;
            _hubContext = hubContext;
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

                // بررسی عضویت درخواست کننده
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
                    members.Add(new GroupMemberDto
                    {
                        UserId = participant.UserId,
                        Username = participant.User.UserName ?? "",
                        FirstName = participant.User.FirstName,
                        LastName = participant.User.LastName,
                        ProfilePictureUrl = participant.User.ProfilePictureUrl,
                        Role = participant.Role,
                        JoinedAt = participant.JoinedAt,
                        IsOnline = isOnline,
                        LastSeen = participant.User.LastSeenAt
                    });
                }

                return new GroupInfoDto
                {
                    Id = chat.Id.ToString(),
                    Title = chat.Title ?? "",
                    Description = chat.Description,
                    GroupImageUrl = chat.GroupImageUrl,
                    OwnerId = chat.OwnerId ?? 0,
                    OwnerName = owner?.UserName ?? "",
                    CreatedAt = chat.CreatedAt,
                    MembersCount = members.Count,
                    Members = members.OrderBy(m => m.Role == "Owner" ? 0 : m.Role == "Admin" ? 1 : 2)
                                   .ThenBy(m => m.Username)
                                   .ToList(),
                    Settings = new GroupSettingsDto
                    {
                        MaxMembers = chat.MaxMembers,
                        OnlyAdminsCanSendMessages = chat.OnlyAdminsCanSendMessages,
                        OnlyAdminsCanAddMembers = chat.OnlyAdminsCanAddMembers,
                        OnlyAdminsCanEditInfo = chat.OnlyAdminsCanEditGroupInfo
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group info for chat {ChatId}", chatId);
                return null;
            }
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

                // بررسی دسترسی
                var hasPermission = await HasUpdateInfoPermissionAsync(chatId, requesterId);
                if (!hasPermission)
                {
                    _logger.LogWarning("User {UserId} doesn't have permission to update group info for chat {ChatId}", requesterId, chatId);
                    return false;
                }

                // به‌روزرسانی اطلاعات گروه
                chat.Title = dto.Title;
                chat.Description = dto.Description;
                chat.GroupImageUrl = dto.GroupImageUrl;

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();

                // اطلاع‌رسانی به اعضای گروه
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

                // فقط مالک می‌تواند تنظیمات را تغییر دهد
                if (chat.OwnerId != requesterId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", requesterId, chatId);
                    return false;
                }

                // Validate settings
                if (settings.MaxMembers < 2 || settings.MaxMembers > 10000)
                {
                    _logger.LogWarning("Invalid MaxMembers value: {MaxMembers}", settings.MaxMembers);
                    return false;
                }

                chat.MaxMembers = settings.MaxMembers;
                chat.OnlyAdminsCanSendMessages = settings.OnlyAdminsCanSendMessages;
                chat.OnlyAdminsCanAddMembers = settings.OnlyAdminsCanAddMembers;
                chat.OnlyAdminsCanEditGroupInfo = settings.OnlyAdminsCanEditInfo;

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();

                // اطلاع‌رسانی به اعضای گروه
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
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                {
                    _logger.LogWarning("Group {ChatId} not found or is not a group", chatId);
                    return false;
                }

                // بررسی دسترسی اضافه کردن عضو
                var hasPermission = await HasAddMemberPermissionAsync(chatId, adminId);
                if (!hasPermission)
                {
                    _logger.LogWarning("User {UserId} doesn't have permission to add members to group {ChatId}", adminId, chatId);
                    return false;
                }

                // بررسی محدودیت تعداد اعضا
                var currentMembersCount = chat.Participants.Count(p => p.IsActive);
                if (currentMembersCount + userIds.Count > chat.MaxMembers)
                {
                    _logger.LogWarning("Adding {Count} members would exceed max members limit of {MaxMembers}", userIds.Count, chat.MaxMembers);
                    return false;
                }

                // اضافه کردن اعضا
                var addedMembers = new List<long>();
                foreach (var userId in userIds)
                {
                    // بررسی اینکه کاربر قبلاً عضو نباشد
                    if (!chat.Participants.Any(p => p.UserId == userId && p.IsActive))
                    {
                        await _unitOfWork.ChatRepository.AddParticipantAsync(chatId, userId);
                        addedMembers.Add(userId);
                    }
                }

                if (addedMembers.Any())
                {
                    await _unitOfWork.CompleteAsync();

                    // اطلاع‌رسانی به اعضای گروه
                    await NotifyGroupMembersAsync(chatId, "MembersAdded", new
                    {
                        chatId = chatId.ToString(),
                        addedMembers = addedMembers,
                        addedBy = adminId
                    });

                    _logger.LogInformation("Added {Count} members to group {ChatId} by user {UserId}", addedMembers.Count, chatId, adminId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding members to group {ChatId} by user {UserId}", chatId, adminId);
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

                // بررسی دسترسی حذف عضو
                var hasPermission = await HasRemoveMemberPermissionAsync(chatId, adminId);
                if (!hasPermission)
                {
                    _logger.LogWarning("User {UserId} doesn't have permission to remove members from group {ChatId}", adminId, chatId);
                    return false;
                }

                // نمی‌توان مالک را حذف کرد
                if (memberId == chat.OwnerId)
                {
                    _logger.LogWarning("Cannot remove owner {UserId} from group {ChatId}", memberId, chatId);
                    return false;
                }

                // بررسی اینکه عضو واقعاً در گروه باشد
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, memberId))
                {
                    _logger.LogWarning("User {UserId} is not a member of group {ChatId}", memberId, chatId);
                    return false;
                }

                await _unitOfWork.ChatRepository.RemoveParticipantAsync(chatId, memberId);
                await _unitOfWork.CompleteAsync();

                // اطلاع‌رسانی به اعضای گروه
                await NotifyGroupMembersAsync(chatId, "MemberRemoved", new
                {
                    chatId = chatId.ToString(),
                    removedMember = memberId,
                    removedBy = adminId
                });

                _logger.LogInformation("Member {MemberId} removed from group {ChatId} by user {UserId}", memberId, chatId, adminId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {MemberId} from group {ChatId} by user {UserId}", memberId, chatId, adminId);
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

                // فقط مالک می‌تواند نقش‌ها را تغییر دهد
                if (chat.OwnerId != adminId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", adminId, chatId);
                    return false;
                }

                // نمی‌توان نقش مالک را تغییر داد
                if (memberId == chat.OwnerId)
                {
                    _logger.LogWarning("Cannot change role of owner {UserId} in group {ChatId}", memberId, chatId);
                    return false;
                }

                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, memberId);
                if (participant == null)
                {
                    _logger.LogWarning("Participant {UserId} not found in group {ChatId}", memberId, chatId);
                    return false;
                }

                var oldRole = participant.Role;
                participant.Role = newRole.ToString();
                await _unitOfWork.CompleteAsync();

                // اطلاع‌رسانی به اعضای گروه
                await NotifyGroupMembersAsync(chatId, "MemberRoleUpdated", new
                {
                    chatId = chatId.ToString(),
                    memberId = memberId,
                    oldRole = oldRole,
                    newRole = newRole.ToString(),
                    updatedBy = adminId
                });

                _logger.LogInformation("Role of member {MemberId} changed from {OldRole} to {NewRole} in group {ChatId} by user {UserId}",
                    memberId, oldRole, newRole, chatId, adminId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member role for {MemberId} in group {ChatId} by user {UserId}", memberId, chatId, adminId);
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

                // مالک نمی‌تواند گروه را ترک کند مگر اینکه مالکیت را منتقل کند
                if (chat.OwnerId == userId)
                {
                    _logger.LogWarning("Owner {UserId} cannot leave group {ChatId} without transferring ownership", userId, chatId);
                    return false;
                }

                // بررسی اینکه کاربر عضو گروه باشد
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId))
                {
                    _logger.LogWarning("User {UserId} is not a member of group {ChatId}", userId, chatId);
                    return false;
                }

                await _unitOfWork.ChatRepository.RemoveParticipantAsync(chatId, userId);
                await _unitOfWork.CompleteAsync();

                // اطلاع‌رسانی به اعضای گروه
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
                _logger.LogError(ex, "Error when user {UserId} leaving group {ChatId}", userId, chatId);
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

                // فقط مالک می‌تواند گروه را حذف کند
                if (chat.OwnerId != ownerId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", ownerId, chatId);
                    return false;
                }

                // اطلاع‌رسانی به اعضای گروه قبل از حذف
                await NotifyGroupMembersAsync(chatId, "GroupDeleted", new
                {
                    chatId = chatId.ToString(),
                    deletedBy = ownerId
                });

                // حذف کلیه پیام‌ها
                await _unitOfWork.MessageRepository.DeleteAllMessagesAsync(chatId);

                // حذف اعضا
                await _unitOfWork.GroupMemberRepository.DeleteAllMembersAsync(chatId);

                // حذف تنظیمات گروه
                await _unitOfWork.GroupSettingsRepository.DeleteSettingsAsync(chatId);

                // حذف گروه
                await _unitOfWork.ChatRepository.DeleteAsync(chat);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Group {ChatId} deleted by owner {UserId}", chatId, ownerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {ChatId} by owner {UserId}", chatId, ownerId);
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

                // فقط مالک فعلی می‌تواند مالکیت را منتقل کند
                if (chat.OwnerId != currentOwnerId)
                {
                    _logger.LogWarning("User {UserId} is not the owner of group {ChatId}", currentOwnerId, chatId);
                    return false;
                }

                // بررسی اینکه مالک جدید عضو گروه باشد
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, newOwnerId))
                {
                    _logger.LogWarning("New owner {UserId} is not a member of group {ChatId}", newOwnerId, chatId);
                    return false;
                }

                // تغییر مالک
                chat.OwnerId = newOwnerId;

                // تغییر نقش مالک جدید به Owner
                var newOwnerParticipant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, newOwnerId);
                if (newOwnerParticipant != null)
                {
                    newOwnerParticipant.Role = "Owner";
                }

                // تغییر نقش مالک قبلی به Admin
                var oldOwnerParticipant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, currentOwnerId);
                if (oldOwnerParticipant != null)
                {
                    oldOwnerParticipant.Role = "Admin";
                }

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();

                // اطلاع‌رسانی به اعضای گروه
                await NotifyGroupMembersAsync(chatId, "OwnershipTransferred", new
                {
                    chatId = chatId.ToString(),
                    oldOwner = currentOwnerId,
                    newOwner = newOwnerId
                });

                _logger.LogInformation("Ownership of group {ChatId} transferred from {OldOwner} to {NewOwner}", chatId, currentOwnerId, newOwnerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring ownership of group {ChatId} from {OldOwner} to {NewOwner}", chatId, currentOwnerId, newOwnerId);
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

                // بررسی عضویت درخواست کننده
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, requesterId))
                {
                    _logger.LogWarning("User {UserId} is not a member of group {ChatId}", requesterId, chatId);
                    return new List<GroupMemberDto>();
                }

                var members = new List<GroupMemberDto>();

                foreach (var participant in chat.Participants.Where(p => p.IsActive))
                {
                    var isOnline = await _userConnectionService.IsUserOnlineAsync(participant.UserId);
                    members.Add(new GroupMemberDto
                    {
                        UserId = participant.UserId,
                        Username = participant.User.UserName ?? "",
                        FirstName = participant.User.FirstName,
                        LastName = participant.User.LastName,
                        ProfilePictureUrl = participant.User.ProfilePictureUrl,
                        Role = participant.Role,
                        JoinedAt = participant.JoinedAt,
                        IsOnline = isOnline,
                        LastSeen = participant.User.LastSeenAt
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

                // Check if user is owner
                if (chat.OwnerId == userId)
                    return true;

                // Check group settings
                if (!chat.OnlyAdminsCanAddMembers)
                    return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);

                // Check if user is admin
                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, userId);
                return participant?.Role == "Admin";
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

                // Only owner and admins can remove members
                if (chat.OwnerId == userId)
                    return true;

                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, userId);
                return participant?.Role == "Admin";
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

                // Check if user is owner
                if (chat.OwnerId == userId)
                    return true;

                // Check group settings
                if (!chat.OnlyAdminsCanEditGroupInfo)
                    return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);

                // Check if user is admin
                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, userId);
                return participant?.Role == "Admin";
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