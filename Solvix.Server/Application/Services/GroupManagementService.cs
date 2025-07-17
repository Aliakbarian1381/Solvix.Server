using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.Application.Services
{
    public class GroupManagementService : IGroupManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GroupManagementService> _logger;
        private readonly IUserConnectionService _userConnectionService;

        public GroupManagementService(
            IUnitOfWork unitOfWork,
            ILogger<GroupManagementService> logger,
            IUserConnectionService userConnectionService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userConnectionService = userConnectionService;
        }

        public async Task<GroupInfoDto?> GetGroupInfoAsync(Guid chatId, long requesterId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return null;

                // بررسی عضویت درخواست کننده
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, requesterId))
                    return null;

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
                    Id = chat.Id,
                    Title = chat.Title ?? "",
                    Description = chat.Description,
                    GroupImageUrl = chat.GroupImageUrl,
                    OwnerId = chat.OwnerId ?? 0,
                    OwnerName = owner?.UserName ?? "",
                    CreatedAt = chat.CreatedAt,
                    MembersCount = members.Count,
                    Settings = new GroupSettingsDto
                    {
                        OnlyAdminsCanSendMessages = chat.OnlyAdminsCanSendMessages,
                        OnlyAdminsCanAddMembers = chat.OnlyAdminsCanAddMembers,
                        OnlyAdminsCanEditGroupInfo = chat.OnlyAdminsCanEditGroupInfo,
                        MaxMembers = chat.MaxMembers
                    },
                    Members = members.OrderByDescending(m => m.Role).ThenBy(m => m.JoinedAt).ToList()
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
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // بررسی مجوز - فقط ادمین‌ها و مالک
                var requesterParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == requesterId && p.IsActive);

                if (requesterParticipant == null)
                    return false;

                bool canEdit = requesterParticipant.Role >= GroupRole.Admin ||
                              (chat.OnlyAdminsCanEditGroupInfo == false && requesterParticipant.Role == GroupRole.Member);

                if (!canEdit)
                    return false;

                // به‌روزرسانی اطلاعات
                if (!string.IsNullOrWhiteSpace(dto.Title))
                    chat.Title = dto.Title.Trim();

                if (dto.Description != null)
                    chat.Description = dto.Description.Trim();

                if (dto.GroupImageUrl != null)
                    chat.GroupImageUrl = dto.GroupImageUrl;

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Group {ChatId} info updated by user {UserId}", chatId, requesterId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group info for chat {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> UpdateGroupSettingsAsync(Guid chatId, long requesterId, GroupSettingsDto settings)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // بررسی مجوز - فقط مالک و ادمین‌ها
                var requesterParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == requesterId && p.IsActive);

                if (requesterParticipant == null || requesterParticipant.Role < GroupRole.Admin)
                    return false;

                // به‌روزرسانی تنظیمات
                chat.OnlyAdminsCanSendMessages = settings.OnlyAdminsCanSendMessages;
                chat.OnlyAdminsCanAddMembers = settings.OnlyAdminsCanAddMembers;
                chat.OnlyAdminsCanEditGroupInfo = settings.OnlyAdminsCanEditGroupInfo;
                chat.MaxMembers = settings.MaxMembers;

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Group {ChatId} settings updated by user {UserId}", chatId, requesterId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group settings for chat {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> AddMembersAsync(Guid chatId, long adminId, List<long> userIds)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // بررسی مجوز
                var adminParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == adminId && p.IsActive);

                if (adminParticipant == null)
                    return false;

                bool canAdd = adminParticipant.Role >= GroupRole.Admin ||
                             (chat.OnlyAdminsCanAddMembers == false);

                if (!canAdd)
                    return false;

                // بررسی حداکثر تعداد اعضا
                var currentMembersCount = chat.Participants.Count(p => p.IsActive);
                if (currentMembersCount + userIds.Count > chat.MaxMembers)
                    return false;

                // اضافه کردن اعضای جدید
                foreach (var userId in userIds.Distinct())
                {
                    // بررسی عدم عضویت قبلی
                    var existingParticipant = chat.Participants
                        .FirstOrDefault(p => p.UserId == userId);

                    if (existingParticipant == null)
                    {
                        await _unitOfWork.ChatRepository.AddParticipantAsync(chatId, userId);
                    }
                    else if (!existingParticipant.IsActive)
                    {
                        // فعال‌سازی مجدد عضو
                        existingParticipant.IsActive = true;
                        existingParticipant.JoinedAt = DateTime.UtcNow;
                        existingParticipant.LeftAt = null;
                    }
                }

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Users {UserIds} added to group {ChatId} by {AdminId}",
                    string.Join(", ", userIds), chatId, adminId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding members to group {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> RemoveMemberAsync(Guid chatId, long adminId, long memberId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // بررسی مجوز
                var adminParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == adminId && p.IsActive);
                var memberParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == memberId && p.IsActive);

                if (adminParticipant == null || memberParticipant == null)
                    return false;

                // مالک نمی‌تواند حذف شود
                if (memberParticipant.Role == GroupRole.Owner)
                    return false;

                // فقط ادمین‌ها می‌توانند اعضا را حذف کنند
                if (adminParticipant.Role < GroupRole.Admin)
                    return false;

                // ادمین نمی‌تواند ادمین دیگر را حذف کند (فقط مالک)
                if (memberParticipant.Role == GroupRole.Admin && adminParticipant.Role != GroupRole.Owner)
                    return false;

                // حذف عضو (soft delete)
                memberParticipant.IsActive = false;
                memberParticipant.LeftAt = DateTime.UtcNow;

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("User {MemberId} removed from group {ChatId} by {AdminId}",
                    memberId, chatId, adminId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member from group {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> UpdateMemberRoleAsync(Guid chatId, long adminId, long memberId, GroupRole newRole)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                var adminParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == adminId && p.IsActive);
                var memberParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == memberId && p.IsActive);

                if (adminParticipant == null || memberParticipant == null)
                    return false;

                // فقط مالک می‌تواند نقش‌ها را تغییر دهد
                if (adminParticipant.Role != GroupRole.Owner)
                    return false;

                // نمی‌توان نقش مالک را تغییر داد
                if (memberParticipant.Role == GroupRole.Owner || newRole == GroupRole.Owner)
                    return false;

                memberParticipant.Role = newRole;
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("User {MemberId} role updated to {NewRole} in group {ChatId} by {AdminId}",
                    memberId, newRole, chatId, adminId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member role in group {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> LeaveGroupAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                var participant = chat.Participants
                    .FirstOrDefault(p => p.UserId == userId && p.IsActive);

                if (participant == null)
                    return false;

                // مالک نمی‌تواند گروه را ترک کند (باید مالکیت را منتقل کند)
                if (participant.Role == GroupRole.Owner)
                    return false;

                // خروج از گروه
                participant.IsActive = false;
                participant.LeftAt = DateTime.UtcNow;

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("User {UserId} left group {ChatId}", userId, chatId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving group {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> DeleteGroupAsync(Guid chatId, long ownerId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // فقط مالک می‌تواند گروه را حذف کند
                if (chat.OwnerId != ownerId)
                    return false;

                // حذف گروه
                await _unitOfWork.ChatRepository.DeleteAsync(chat);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Group {ChatId} deleted by owner {OwnerId}", chatId, ownerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> TransferOwnershipAsync(Guid chatId, long currentOwnerId, long newOwnerId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // بررسی مالکیت فعلی
                if (chat.OwnerId != currentOwnerId)
                    return false;

                var newOwnerParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == newOwnerId && p.IsActive);

                if (newOwnerParticipant == null)
                    return false;

                // انتقال مالکیت
                chat.OwnerId = newOwnerId;
                newOwnerParticipant.Role = GroupRole.Owner;

                // تبدیل مالک قبلی به ادمین
                var oldOwnerParticipant = chat.Participants
                    .FirstOrDefault(p => p.UserId == currentOwnerId && p.IsActive);
                if (oldOwnerParticipant != null)
                {
                    oldOwnerParticipant.Role = GroupRole.Admin;
                }

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Ownership of group {ChatId} transferred from {OldOwnerId} to {NewOwnerId}",
                    chatId, currentOwnerId, newOwnerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring ownership of group {ChatId}", chatId);
                return false;
            }
        }

        public async Task<List<GroupMemberDto>> GetGroupMembersAsync(Guid chatId, long requesterId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return new List<GroupMemberDto>();

                // بررسی عضویت
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, requesterId))
                    return new List<GroupMemberDto>();

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

                return members.OrderByDescending(m => m.Role).ThenBy(m => m.JoinedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group members for chat {ChatId}", chatId);
                return new List<GroupMemberDto>();
            }
        }
    }
}
