using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Core.Entities;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.API.Hubs;
using SolvixMessage = Solvix.Server.Core.Entities.Message;

namespace Solvix.Server.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatService> _logger;
        private readonly INotificationService _notificationService;

        public ChatService(
            IUnitOfWork unitOfWork,
            IHubContext<ChatHub> hubContext,
            IUserConnectionService userConnectionService,
            ILogger<ChatService> logger,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
            _logger = logger;
            _notificationService = notificationService;
        }

        #region Public Chat Methods

        public async Task<List<ChatDto>> GetUserChatsAsync(long userId)
        {
            try
            {
                var chats = await _unitOfWork.ChatRepository.GetUserChatsAsync(userId);

                var participantIds = chats
                    .SelectMany(c => c.Participants)
                    .Select(p => p.UserId)
                    .Where(id => id != userId)
                    .Distinct()
                    .ToList();

                var onlineStatuses = new Dictionary<long, bool>();

                foreach (var id in participantIds)
                {
                    onlineStatuses[id] = await _userConnectionService.IsUserOnlineAsync(id);
                }

                onlineStatuses[userId] = true;

                var chatDtos = chats.Select(chat => MappingHelper.MapToChatDto(chat, userId, onlineStatuses)).ToList();

                return chatDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chats for user {UserId}", userId);
                return new List<ChatDto>();
            }
        }

        public async Task<ChatDto?> GetChatByIdAsync(Guid chatId, long userId)
        {
            try
            {
                _logger.LogInformation("Fetching chat {ChatId} for user {UserId}", chatId, userId);
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null)
                {
                    _logger.LogWarning("Chat {ChatId} not found.", chatId);
                    return null;
                }

                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId))
                {
                    _logger.LogWarning("User {UserId} is not a participant in chat {ChatId}", userId, chatId);
                    return null;
                }

                var participantIds = chat.Participants.Select(p => p.UserId).ToList();
                var onlineStatuses = new Dictionary<long, bool>();

                foreach (var id in participantIds)
                {
                    onlineStatuses[id] = await _userConnectionService.IsUserOnlineAsync(id);
                }

                return MappingHelper.MapToChatDto(chat, userId, onlineStatuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat {ChatId} for user {UserId}", chatId, userId);
                return null;
            }
        }

        public async Task<(Guid chatId, bool alreadyExists)> StartChatWithUserAsync(long initiatorUserId, long recipientUserId)
        {
            try
            {
                var existingChat = await _unitOfWork.ChatRepository.GetPrivateChatBetweenUsersAsync(initiatorUserId, recipientUserId);
                if (existingChat != null)
                {
                    return (existingChat.Id, true);
                }

                var newChat = new Chat
                {
                    Id = Guid.NewGuid(),
                    IsGroup = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.ChatRepository.AddAsync(newChat);

                await _unitOfWork.ChatRepository.AddParticipantAsync(newChat.Id, initiatorUserId);
                await _unitOfWork.ChatRepository.AddParticipantAsync(newChat.Id, recipientUserId);

                await _unitOfWork.CompleteAsync();

                return (newChat.Id, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat between users {InitiatorUserId} and {RecipientUserId}", initiatorUserId, recipientUserId);
                throw;
            }
        }

        public async Task<bool> IsUserParticipantAsync(Guid chatId, long userId)
        {
            return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);
        }

        #endregion

        #region Message Methods

        public async Task<SolvixMessage> SaveMessageAsync(Guid chatId, long senderId, string content)
        {
            try
            {
                // بررسی عضویت کاربر در چت
                var isParticipant = await IsUserParticipantAsync(chatId, senderId);
                if (!isParticipant)
                {
                    _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant.", senderId, chatId);
                    throw new UnauthorizedAccessException("User is not a participant of this chat.");
                }

                var message = new SolvixMessage
                {
                    ChatId = chatId,
                    SenderId = senderId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsEdited = false,
                    IsDeleted = false
                };

                await _unitOfWork.MessageRepository.AddAsync(message);

                // Update chat's last message info
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat != null)
                {
                    chat.LastMessage = content;
                    chat.LastMessageTime = DateTime.UtcNow;
                    await _unitOfWork.ChatRepository.UpdateAsync(chat);
                }

                await _unitOfWork.CompleteAsync();

                // Load sender info
                message.Sender = await _unitOfWork.UserRepository.GetByIdAsync(senderId);

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving message for chat {ChatId}", chatId);
                throw;
            }
        }

        public async Task BroadcastMessageAsync(SolvixMessage message)
        {
            try
            {
                if (message.Sender == null)
                {
                    message.Sender = await _unitOfWork.UserRepository.GetByIdAsync(message.SenderId);
                }

                var messageDto = MappingHelper.MapToMessageDto(message);
                await _hubContext.Clients.Group($"Chat_{message.ChatId}").SendAsync("NewMessage", messageDto);

                // Send push notifications to offline users
                await SendPushNotificationsAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message {MessageId}", message.Id);
            }
        }

        public async Task<List<MessageDto>> GetChatMessagesAsync(Guid chatId, long userId, int skip = 0, int take = 50)
        {
            try
            {
                if (!await IsUserParticipantAsync(chatId, userId))
                {
                    _logger.LogWarning("User {UserId} unauthorized attempt to get messages for Chat {ChatId}.", userId, chatId);
                    throw new UnauthorizedAccessException("User is not a participant of this chat.");
                }

                var messages = await _unitOfWork.MessageRepository.GetChatMessagesAsync(chatId, skip, take);
                return messages.Select(MappingHelper.MapToMessageDto).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for chat {ChatId} by user {UserId}", chatId, userId);
                return new List<MessageDto>();
            }
        }

        public async Task MarkMessageAsReadAsync(int messageId, long readerUserId)
        {
            try
            {
                var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                if (message == null || message.SenderId == readerUserId)
                {
                    return;
                }

                if (!await IsUserParticipantAsync(message.ChatId, readerUserId))
                {
                    return;
                }

                await _unitOfWork.MessageRepository.MarkAsReadAsync(messageId, readerUserId);
                await _unitOfWork.CompleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read for user {UserId}", messageId, readerUserId);
            }
        }

        public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId)
        {
            try
            {
                await _unitOfWork.MessageRepository.MarkMultipleAsReadAsync(messageIds, readerUserId);
                await _unitOfWork.CompleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple messages as read for user {UserId}", readerUserId);
            }
        }

        public async Task<SolvixMessage?> EditMessageAsync(int messageId, string newContent, long editorUserId)
        {
            try
            {
                var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                if (message == null || message.SenderId != editorUserId)
                {
                    return null;
                }

                message.Content = newContent;
                message.EditedAt = DateTime.UtcNow;
                message.IsEdited = true;

                await _unitOfWork.MessageRepository.UpdateAsync(message);
                await _unitOfWork.CompleteAsync();

                // Load sender info
                message.Sender = await _unitOfWork.UserRepository.GetByIdAsync(message.SenderId);

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<SolvixMessage?> DeleteMessageAsync(int messageId, long deleterUserId)
        {
            try
            {
                var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                if (message == null)
                {
                    return null;
                }

                // Check if user can delete this message
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(message.ChatId);
                if (chat == null)
                {
                    return null;
                }

                bool canDelete = message.SenderId == deleterUserId ||
                               (chat.IsGroup && await IsGroupOwnerAsync(message.ChatId, deleterUserId));

                if (!canDelete)
                {
                    return null;
                }

                message.IsDeleted = true;
                message.DeletedAt = DateTime.UtcNow;
                message.Content = "این پیام حذف شده است";

                await _unitOfWork.MessageRepository.UpdateAsync(message);
                await _unitOfWork.CompleteAsync();

                // Load sender info
                message.Sender = await _unitOfWork.UserRepository.GetByIdAsync(message.SenderId);

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                return null;
            }
        }

        public async Task BroadcastMessageUpdateAsync(SolvixMessage message)
        {
            try
            {
                if (message.Sender == null)
                {
                    message.Sender = await _unitOfWork.UserRepository.GetByIdAsync(message.SenderId);
                }

                var messageDto = MappingHelper.MapToMessageDto(message);
                await _hubContext.Clients.Group($"Chat_{message.ChatId}").SendAsync("MessageUpdated", messageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message update {MessageId}", message.Id);
            }
        }

        #endregion

        #region Group Chat Methods

        public async Task<ChatDto> CreateGroupChatAsync(long creatorId, string title, List<long> participantIds)
        {
            try
            {
                var groupChat = new Chat
                {
                    Id = Guid.NewGuid(),
                    IsGroup = true,
                    Title = title,
                    OwnerId = creatorId,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 256,
                    OnlyAdminsCanAddMembers = false,
                    OnlyAdminsCanEditGroupInfo = true,
                    OnlyAdminsCanSendMessages = false
                };

                await _unitOfWork.ChatRepository.AddAsync(groupChat);

                // Add creator as owner
                await _unitOfWork.ChatRepository.AddParticipantAsync(groupChat.Id, creatorId);

                // Add other participants
                foreach (var participantId in participantIds.Where(id => id != creatorId))
                {
                    await _unitOfWork.ChatRepository.AddParticipantAsync(groupChat.Id, participantId);
                }

                await _unitOfWork.CompleteAsync();

                // Load participants and online statuses
                var updatedChat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(groupChat.Id);
                var onlineStatuses = new Dictionary<long, bool>();

                foreach (var participant in updatedChat.Participants)
                {
                    onlineStatuses[participant.UserId] = await _userConnectionService.IsUserOnlineAsync(participant.UserId);
                }

                return MappingHelper.MapToChatDto(updatedChat, creatorId, onlineStatuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group chat with title {Title}", title);
                throw;
            }
        }

        public async Task<bool> HasAddMemberPermissionAsync(Guid chatId, long userId)
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
                    return await IsUserParticipantAsync(chatId, userId);

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

        public async Task<bool> HasRemoveMemberPermissionAsync(Guid chatId, long userId)
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

        public async Task<bool> HasChangeRolePermissionAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    return false;

                // Only owner can change roles
                return chat.OwnerId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking change role permission for chat {ChatId} and user {UserId}", chatId, userId);
                return false;
            }
        }

        public async Task<bool> IsGroupOwnerAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                return chat?.OwnerId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is owner of chat {ChatId}", userId, chatId);
                return false;
            }
        }

        public async Task AddMemberToGroupAsync(Guid chatId, long memberId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("گروه یافت نشد");

                // بررسی اینکه کاربر قبلاً عضو نباشد
                if (await IsUserParticipantAsync(chatId, memberId))
                    throw new ArgumentException("کاربر قبلاً عضو گروه است");

                // اضافه کردن عضو جدید
                await _unitOfWork.ChatRepository.AddParticipantAsync(chatId, memberId);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("User {MemberId} added to group {ChatId}", memberId, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member {MemberId} to group {ChatId}", memberId, chatId);
                throw;
            }
        }

        public async Task RemoveMemberFromGroupAsync(Guid chatId, long memberId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("گروه یافت نشد");

                // نمی‌توان مالک را حذف کرد
                if (memberId == chat.OwnerId)
                    throw new ArgumentException("نمی‌توان مالک گروه را حذف کرد");

                await _unitOfWork.ChatRepository.RemoveParticipantAsync(chatId, memberId);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("User {MemberId} removed from group {ChatId}", memberId, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {MemberId} from group {ChatId}", memberId, chatId);
                throw;
            }
        }

        public async Task ChangeMemberRoleAsync(Guid chatId, long memberId, string newRole)
        {
            try
            {
                var participant = await _unitOfWork.ChatRepository.GetParticipantAsync(chatId, memberId);
                if (participant == null)
                    throw new ArgumentException("عضو یافت نشد");

                participant.Role = newRole;
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Role of member {MemberId} changed to {NewRole} in group {ChatId}",
                    memberId, newRole, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing role of member {MemberId} in group {ChatId}", memberId, chatId);
                throw;
            }
        }

        public async Task LeaveGroupAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("گروه یافت نشد");

                // مالک نمی‌تواند گروه را ترک کند
                if (userId == chat.OwnerId)
                    throw new ArgumentException("مالک نمی‌تواند گروه را ترک کند");

                await _unitOfWork.ChatRepository.RemoveParticipantAsync(chatId, userId);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("User {UserId} left group {ChatId}", userId, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when user {UserId} leaving group {ChatId}", userId, chatId);
                throw;
            }
        }

        public async Task DeleteGroupAsync(Guid chatId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("گروه یافت نشد");

                // حذف همه پیام‌ها
                var messages = await _unitOfWork.MessageRepository.ListAsync(m => m.ChatId == chatId);
                foreach (var message in messages)
                {
                    await _unitOfWork.MessageRepository.DeleteAsync(message);
                }

                // حذف گروه
                await _unitOfWork.ChatRepository.DeleteAsync(chat);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Group {ChatId} deleted", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {ChatId}", chatId);
                throw;
            }
        }

        public async Task<GroupInfoDto> GetGroupInfoAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("Group not found");

                // Check if user is participant
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId))
                    throw new UnauthorizedAccessException("User is not a participant");

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
                    Members = members,
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
                throw;
            }
        }

        public async Task<GroupSettingsDto> GetGroupSettingsAsync(Guid chatId, long userId)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("Group not found");

                // Check if user is participant
                if (!await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId))
                    throw new UnauthorizedAccessException("User is not a participant");

                return new GroupSettingsDto
                {
                    MaxMembers = chat.MaxMembers,
                    OnlyAdminsCanSendMessages = chat.OnlyAdminsCanSendMessages,
                    OnlyAdminsCanAddMembers = chat.OnlyAdminsCanAddMembers,
                    OnlyAdminsCanEditInfo = chat.OnlyAdminsCanEditGroupInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group settings for chat {ChatId}", chatId);
                throw;
            }
        }

        public async Task UpdateGroupSettingsAsync(Guid chatId, long userId, GroupSettingsDto settings)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
                if (chat == null || !chat.IsGroup)
                    throw new ArgumentException("Group not found");

                // Check permissions
                if (!await HasChangeRolePermissionAsync(chatId, userId))
                    throw new UnauthorizedAccessException("User doesn't have permission to update settings");

                chat.MaxMembers = settings.MaxMembers;
                chat.OnlyAdminsCanSendMessages = settings.OnlyAdminsCanSendMessages;
                chat.OnlyAdminsCanAddMembers = settings.OnlyAdminsCanAddMembers;
                chat.OnlyAdminsCanEditGroupInfo = settings.OnlyAdminsCanEditInfo;

                await _unitOfWork.ChatRepository.UpdateAsync(chat);
                await _unitOfWork.CompleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group settings for chat {ChatId}", chatId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task SendPushNotificationsAsync(SolvixMessage message)
        {
            try
            {
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(message.ChatId);
                if (chat == null) return;

                foreach (var participant in chat.Participants.Where(p => p.UserId != message.SenderId))
                {
                    var isOnline = await _userConnectionService.IsUserOnlineAsync(participant.UserId);
                    if (!isOnline)
                    {
                        var recipientUser = await _unitOfWork.UserRepository.GetByIdAsync(participant.UserId);
                        if (recipientUser != null && !string.IsNullOrEmpty(recipientUser.FcmToken))
                        {
                            var senderName = message.Sender?.UserName ?? "کاربر";
                            var chatTitle = chat.IsGroup ? chat.Title : senderName;

                            // تصحیح: استفاده از FirebaseAdmin.Messaging.Notification
                            var notification = new FirebaseAdmin.Messaging.Notification
                            {
                                Title = chatTitle,
                                Body = message.Content
                            };

                            var notifData = new Dictionary<string, string>
                            {
                                ["chatId"] = message.ChatId.ToString(),
                                ["messageId"] = message.Id.ToString(),
                                ["type"] = "chat_message",
                                ["senderId"] = message.SenderId.ToString(),
                                ["senderName"] = senderName
                            };

                            await _notificationService.SendNotificationAsync(recipientUser, notification, notifData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notifications for message {MessageId}", message.Id);
            }
        }

        #endregion
    }
}