using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using System.Security.Claims;

namespace Solvix.Server.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatHub> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public ChatHub(
            IChatService chatService,
            IUserConnectionService userConnectionService,
            ILogger<ChatHub> logger,
            IUnitOfWork unitOfWork)
        {
            _chatService = chatService;
            _userConnectionService = userConnectionService;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        private long? GetUserIdFromContext()
        {
            var userIdString = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(userIdString, out var uid))
            {
                return uid;
            }
            _logger.LogWarning("Could not parse UserIdentifier '{UserIdentifier}' to long for connection {ConnectionId}.",
                userIdString, Context.ConnectionId);
            return null;
        }

        private long GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
            {
                return userId;
            }
            return 0;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                await _userConnectionService.AddConnectionAsync(userId, Context.ConnectionId);

                // Join user to their personal notification group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

                // Join user to their chats
                await JoinUserChats(userId);

                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                await _userConnectionService.RemoveConnectionAsync(Context.ConnectionId);
                _logger.LogInformation("User {UserId} disconnected with connection {ConnectionId}", userId, Context.ConnectionId);

                if (exception != null)
                {
                    _logger.LogWarning(exception, "User {UserId} disconnected with exception", userId);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        #region Chat Management

        public async Task JoinChatGroup(Guid chatId)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            try
            {
                if (await _chatService.IsUserParticipantAsync(chatId, userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Chat_{chatId}");
                    await Clients.Caller.SendAsync("JoinedChat", chatId);
                    _logger.LogInformation("User {UserId} joined chat group {ChatId}", userId, chatId);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "شما عضو این چت نیستید");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining chat group {ChatId} for user {UserId}", chatId, userId);
                await Clients.Caller.SendAsync("Error", "خطا در اتصال به چت");
            }
        }

        public async Task LeaveChatGroup(Guid chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Chat_{chatId}");
            var userId = GetUserId();
            await Clients.Caller.SendAsync("LeftChat", chatId);
            _logger.LogInformation("User {UserId} left chat group {ChatId}", userId, chatId);
        }

        private async Task JoinUserChats(long userId)
        {
            try
            {
                var userChats = await _chatService.GetUserChatsAsync(userId);
                foreach (var chat in userChats)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Chat_{chat.Id}");
                }
                _logger.LogDebug("User {UserId} joined {ChatCount} chat groups", userId, userChats.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining user chats for user {UserId}", userId);
            }
        }

        #endregion

        #region Message Operations

        public async Task SendToChat(Guid chatId, string messageContent, string? correlationId = null)
        {
            var senderUserId = GetUserIdFromContext();
            if (!senderUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.", correlationId);
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(messageContent))
                {
                    await Clients.Caller.SendAsync("ReceiveError", "متن پیام نمی‌تواند خالی باشد.", correlationId);
                    return;
                }

                if (!await _chatService.IsUserParticipantAsync(chatId, senderUserId.Value))
                {
                    await Clients.Caller.SendAsync("ReceiveError", "شما عضو این چت نیستید.", correlationId);
                    return;
                }

                var message = await _chatService.SaveMessageAsync(chatId, senderUserId.Value, messageContent);
                await _chatService.BroadcastMessageAsync(message);

                // Confirm to sender
                await Clients.Caller.SendAsync("MessageSent", new
                {
                    messageId = message.Id,
                    correlationId = correlationId,
                    sentAt = message.SentAt
                });

                _logger.LogInformation("Message sent by user {UserId} to chat {ChatId}", senderUserId.Value, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ChatId} by user {UserId}", chatId, senderUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در ارسال پیام.", correlationId);
            }
        }

        public async Task EditMessage(int messageId, string newContent)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(newContent))
                {
                    await Clients.Caller.SendAsync("Error", "محتوای جدید نمی‌تواند خالی باشد");
                    return;
                }

                var editedMessage = await _chatService.EditMessageAsync(messageId, newContent, userId);
                if (editedMessage != null)
                {
                    await _chatService.BroadcastMessageUpdateAsync(editedMessage);
                    await Clients.Caller.SendAsync("MessageEdited", new { messageId = messageId });
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "شما اجازه ویرایش این پیام را ندارید");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId}", messageId);
                await Clients.Caller.SendAsync("Error", "خطا در ویرایش پیام");
            }
        }

        public async Task DeleteMessage(int messageId)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            try
            {
                var deletedMessage = await _chatService.DeleteMessageAsync(messageId, userId);
                if (deletedMessage != null)
                {
                    await _chatService.BroadcastMessageUpdateAsync(deletedMessage);
                    await Clients.Caller.SendAsync("MessageDeleted", new { messageId = messageId });
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "شما اجازه حذف این پیام را ندارید");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                await Clients.Caller.SendAsync("Error", "خطا در حذف پیام");
            }
        }

        public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            try
            {
                // Validate that user can mark these messages as read
                foreach (var messageId in messageIds)
                {
                    var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                    if (message != null && await _chatService.IsUserParticipantAsync(message.ChatId, userId))
                    {
                        await _chatService.MarkMessageAsReadAsync(messageId, userId);

                        // Notify chat members about read status
                        await Clients.Group($"Chat_{message.ChatId}")
                            .SendAsync("MessageRead", new { MessageId = messageId, ReaderId = userId, ReadAt = DateTime.UtcNow });
                    }
                }

                await Clients.Caller.SendAsync("MessagesMarkedAsRead", messageIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMultipleMessagesAsReadAsync");
                await Clients.Caller.SendAsync("Error", "Failed to mark messages as read");
            }
        }

        #endregion

        #region Group Management

        public async Task AddMemberToGroup(Guid chatId, long newMemberId)
        {
            var adminUserId = GetUserIdFromContext();
            if (!adminUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.");
                return;
            }

            try
            {
                // بررسی دسترسی ادمین
                var hasPermission = await _chatService.HasAddMemberPermissionAsync(chatId, adminUserId.Value);
                if (!hasPermission)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "شما دسترسی اضافه کردن عضو ندارید.");
                    return;
                }

                // اضافه کردن عضو جدید
                await _chatService.AddMemberToGroupAsync(chatId, newMemberId);

                // اطلاع‌رسانی به همه اعضای گروه
                await Clients.Group($"Chat_{chatId}").SendAsync("MemberAdded", new
                {
                    chatId = chatId,
                    memberId = newMemberId,
                    addedBy = adminUserId.Value
                });

                _logger.LogInformation("User {AdminId} added user {NewMemberId} to group {ChatId}",
                    adminUserId.Value, newMemberId, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member {NewMemberId} to group {ChatId} by admin {AdminId}",
                    newMemberId, chatId, adminUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در اضافه کردن عضو.");
            }
        }

        public async Task RemoveMemberFromGroup(Guid chatId, long memberToRemoveId)
        {
            var adminUserId = GetUserIdFromContext();
            if (!adminUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.");
                return;
            }

            try
            {
                // بررسی دسترسی ادمین
                var hasPermission = await _chatService.HasRemoveMemberPermissionAsync(chatId, adminUserId.Value);
                if (!hasPermission)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "شما دسترسی حذف عضو ندارید.");
                    return;
                }

                // حذف عضو
                await _chatService.RemoveMemberFromGroupAsync(chatId, memberToRemoveId);

                // اطلاع‌رسانی به همه اعضای گروه
                await Clients.Group($"Chat_{chatId}").SendAsync("MemberRemoved", new
                {
                    chatId = chatId,
                    memberId = memberToRemoveId,
                    removedBy = adminUserId.Value
                });

                _logger.LogInformation("User {AdminId} removed user {MemberId} from group {ChatId}",
                    adminUserId.Value, memberToRemoveId, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {MemberId} from group {ChatId} by admin {AdminId}",
                    memberToRemoveId, chatId, adminUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در حذف عضو.");
            }
        }

        public async Task UpdateMemberRole(Guid chatId, long memberId, string newRole)
        {
            var adminUserId = GetUserIdFromContext();
            if (!adminUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.");
                return;
            }

            try
            {
                // بررسی دسترسی ادمین
                var hasPermission = await _chatService.HasChangeRolePermissionAsync(chatId, adminUserId.Value);
                if (!hasPermission)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "شما دسترسی تغییر نقش ندارید.");
                    return;
                }

                // تغییر نقش
                await _chatService.ChangeMemberRoleAsync(chatId, memberId, newRole);

                // اطلاع‌رسانی به همه اعضای گروه
                await Clients.Group($"Chat_{chatId}").SendAsync("MemberRoleUpdated", new
                {
                    chatId = chatId,
                    memberId = memberId,
                    newRole = newRole,
                    updatedBy = adminUserId.Value
                });

                _logger.LogInformation("User {AdminId} updated role of user {MemberId} to {NewRole} in group {ChatId}",
                    adminUserId.Value, memberId, newRole, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member role for {MemberId} in group {ChatId} by admin {AdminId}",
                    memberId, chatId, adminUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در تغییر نقش عضو.");
            }
        }

        #endregion

        #region Typing Indicators

        public async Task StartTyping(Guid chatId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                if (await _chatService.IsUserParticipantAsync(chatId, userId))
                {
                    await Clients.OthersInGroup($"Chat_{chatId}").SendAsync("UserStartedTyping", new
                    {
                        chatId = chatId,
                        userId = userId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartTyping for user {UserId} in chat {ChatId}", userId, chatId);
            }
        }

        public async Task StopTyping(Guid chatId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                if (await _chatService.IsUserParticipantAsync(chatId, userId))
                {
                    await Clients.OthersInGroup($"Chat_{chatId}").SendAsync("UserStoppedTyping", new
                    {
                        chatId = chatId,
                        userId = userId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StopTyping for user {UserId} in chat {ChatId}", userId, chatId);
            }
        }

        #endregion

        #region Status Updates

        public async Task UpdateOnlineStatus(bool isOnline)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                // Update user's online status in database
                var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);
                if (user != null)
                {
                    user.IsOnline = isOnline;
                    user.LastActiveAt = DateTime.UtcNow;
                    if (!isOnline)
                    {
                        user.LastSeenAt = DateTime.UtcNow;
                    }
                    await _unitOfWork.UserRepository.UpdateAsync(user);
                    await _unitOfWork.CompleteAsync();
                }

                // Notify contacts about status change
                await NotifyUserStatusChange(userId, isOnline);

                _logger.LogDebug("User {UserId} status updated to {IsOnline}", userId, isOnline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating online status for user {UserId}", userId);
            }
        }

        private async Task NotifyUserStatusChange(long userId, bool isOnline)
        {
            try
            {
                var contacts = await _unitOfWork.UserContactRepository.GetUserContactsAsync(userId);
                var contactIds = contacts.Select(c => c.ContactUserId).ToList();

                foreach (var contactId in contactIds)
                {
                    await Clients.Group($"User_{contactId}").SendAsync("ContactStatusChanged", new
                    {
                        userId = userId,
                        isOnline = isOnline,
                        lastSeen = !isOnline ? (DateTime?)DateTime.UtcNow : null
                    });
                }

                _logger.LogDebug("User status changed notification sent for user {UserId} (online: {IsOnline}) to {ContactCount} contacts",
                    userId, isOnline, contactIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying user status change for user {UserId}", userId);
            }
        }

        #endregion

        #region Voice/Video Call Support (Future Implementation)

        public async Task InitiateCall(Guid chatId, string callType, string sdpOffer)
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            try
            {
                if (await _chatService.IsUserParticipantAsync(chatId, userId))
                {
                    await Clients.OthersInGroup($"Chat_{chatId}").SendAsync("IncomingCall", new
                    {
                        chatId = chatId,
                        callerId = userId,
                        callType = callType,
                        sdpOffer = sdpOffer
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "شما عضو این چت نیستید");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating call in chat {ChatId} by user {UserId}", chatId, userId);
                await Clients.Caller.SendAsync("Error", "خطا در برقراری تماس");
            }
        }

        public async Task AcceptCall(Guid chatId, long callerId, string sdpAnswer)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                await Clients.Group($"User_{callerId}").SendAsync("CallAccepted", new
                {
                    chatId = chatId,
                    accepterId = userId,
                    sdpAnswer = sdpAnswer
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting call from {CallerId} in chat {ChatId}", callerId, chatId);
            }
        }

        public async Task RejectCall(Guid chatId, long callerId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                await Clients.Group($"User_{callerId}").SendAsync("CallRejected", new
                {
                    chatId = chatId,
                    rejecterId = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting call from {CallerId} in chat {ChatId}", callerId, chatId);
            }
        }

        public async Task EndCall(Guid chatId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                await Clients.Group($"Chat_{chatId}").SendAsync("CallEnded", new
                {
                    chatId = chatId,
                    endedBy = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending call in chat {ChatId}", chatId);
            }
        }

        #endregion
    }
}