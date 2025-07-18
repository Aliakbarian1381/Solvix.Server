using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Infrastructure.Repositories;
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

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId > 0)
            {
                await _userConnectionService.AddConnectionAsync(userId, Context.ConnectionId);
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
            }
            await base.OnDisconnectedAsync(exception);
        }

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
            _logger.LogInformation("User {UserId} left chat group {ChatId}", userId, chatId);
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


        public async Task SendToChat(Guid chatId, string messageContent, string correlationId)
        {
            var senderUserId = GetUserIdFromContext();
            if (!senderUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت. امکان ارسال پیام وجود ندارد.");
                return;
            }

            // اعتبارسنجی correlationId
            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
            }

            try
            {
                // ذخیره پیام و دریافت نسخه ذخیره شده
                var savedMessage = await _chatService.SaveMessageAsync(chatId, senderUserId.Value, messageContent);

                // ارسال تأییدیه همبستگی به فرستنده
                var messageDto = MappingHelper.MapToMessageDto(savedMessage);
                await Clients.Caller.SendAsync("MessageCorrelationConfirmation", correlationId, messageDto);

                // انتشار پیام به همه اعضای چت
                await _chatService.BroadcastMessageAsync(savedMessage);

                _logger.LogInformation("Message sent to chat {ChatId} by user {UserId} with correlation {CorrelationId}, assigned ID {MessageId}",
                    chatId, senderUserId.Value, correlationId, savedMessage.Id);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant. Error: {Error}",
                    senderUserId.Value, chatId, ex.Message);
                await Clients.Caller.SendAsync("ReceiveError", "شما عضو این چت نیستید.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ChatId} by user {UserId}", chatId, senderUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در ارسال پیام.");
            }
        }

        public async Task JoinGroup(Guid chatId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue) return;

            try
            {
                // بررسی عضویت کاربر در چت
                if (await _chatService.IsUserParticipantAsync(chatId, userId.Value))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, chatId.ToString());
                    _logger.LogInformation("User {UserId} joined group {ChatId} with connection {ConnectionId}",
                        userId.Value, chatId, Context.ConnectionId);
                }
                else
                {
                    _logger.LogWarning("User {UserId} attempted to join group {ChatId} without being a participant",
                        userId.Value, chatId);
                    await Clients.Caller.SendAsync("ReceiveError", "شما عضو این چت نیستید.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining group {ChatId} for user {UserId}", chatId, userId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در اتصال به چت.");
            }
        }

        public async Task LeaveGroup(Guid chatId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue) return;

            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());
                _logger.LogInformation("User {UserId} left group {ChatId} with connection {ConnectionId}",
                    userId.Value, chatId, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving group {ChatId} for user {UserId}", chatId, userId.Value);
            }
        }

        public async Task NotifyTyping(Guid chatId, bool isTyping)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            try
            {
                if (await _chatService.IsUserParticipantAsync(chatId, userId))
                {
                    await Clients.OthersInGroup($"Chat_{chatId}").SendAsync("UserTyping", new
                    {
                        ChatId = chatId,
                        UserId = userId,
                        IsTyping = isTyping
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying typing for user {UserId} in chat {ChatId}", userId, chatId);
            }
        }


        // ✅ متد جدید برای مدیریت گروه - اضافه کردن عضو
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
                await Clients.Group(chatId.ToString()).SendAsync("MemberAdded", chatId, newMemberId);

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
                // اینجا باید chatId هم پاس بدین
                // اما چون از signature قدیمی استفاده می‌کنین، باید اول پیام رو دریافت کنین
                foreach (var messageId in messageIds)
                {
                    var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                    if (message != null)
                    {
                        await _chatService.MarkMultipleMessagesAsReadAsync(new List<int> { messageId }, userId);
                        await Clients.Group($"Chat_{message.ChatId}")
                            .SendAsync("MessageRead", new { MessageId = messageId, ReaderId = userId });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMultipleMessagesAsReadAsync");
                await Clients.Caller.SendAsync("Error", "Failed to mark messages as read");
            }
        }

        // ✅ متد جدید برای مدیریت گروه - حذف عضو
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
                await Clients.Group(chatId.ToString()).SendAsync("MemberRemoved", chatId, memberToRemoveId);

                // حذف عضو از گروه SignalR
                var memberConnections = await _userConnectionService.GetConnectionsForUserAsync(memberToRemoveId);
                foreach (var connectionId in memberConnections)
                {
                    await Groups.RemoveFromGroupAsync(connectionId, chatId.ToString());
                }

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

        // ✅ متد جدید برای تغییر نقش عضو
        public async Task ChangeMemberRole(Guid chatId, long memberId, string newRole)
        {
            var adminUserId = GetUserIdFromContext();
            if (!adminUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.");
                return;
            }

            try
            {
                // بررسی دسترسی تغییر نقش
                var hasPermission = await _chatService.HasChangeRolePermissionAsync(chatId, adminUserId.Value);
                if (!hasPermission)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "شما دسترسی تغییر نقش ندارید.");
                    return;
                }

                // تغییر نقش عضو
                await _chatService.ChangeMemberRoleAsync(chatId, memberId, newRole);

                // اطلاع‌رسانی به همه اعضای گروه
                await Clients.Group(chatId.ToString()).SendAsync("MemberRoleChanged", chatId, memberId, newRole);

                _logger.LogInformation("User {AdminId} changed role of user {MemberId} to {NewRole} in group {ChatId}",
                    adminUserId.Value, memberId, newRole, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing role of member {MemberId} to {NewRole} in group {ChatId} by admin {AdminId}",
                    memberId, newRole, chatId, adminUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در تغییر نقش عضو.");
            }
        }

        // ✅ متد جدید برای خروج از گروه
        public async Task LeaveGroupChat(Guid chatId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.");
                return;
            }

            try
            {
                // بررسی عضویت کاربر در چت
                if (!await _chatService.IsUserParticipantAsync(chatId, userId.Value))
                {
                    await Clients.Caller.SendAsync("ReceiveError", "شما عضو این چت نیستید.");
                    return;
                }

                // خروج از گروه
                await _chatService.LeaveGroupAsync(chatId, userId.Value);

                // حذف از گروه SignalR
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());

                // اطلاع‌رسانی به سایر اعضای گروه
                await Clients.Group(chatId.ToString()).SendAsync("MemberLeft", chatId, userId.Value);

                // تأیید خروج به کاربر
                await Clients.Caller.SendAsync("GroupLeft", chatId);

                _logger.LogInformation("User {UserId} left group {ChatId}", userId.Value, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving group {ChatId} for user {UserId}", chatId, userId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در خروج از گروه.");
            }
        }

        // ✅ متد جدید برای حذف گروه (فقط مالک)
        public async Task DeleteGroup(Guid chatId)
        {
            var ownerId = GetUserIdFromContext();
            if (!ownerId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت.");
                return;
            }

            try
            {
                // بررسی مالکیت گروه
                var isOwner = await _chatService.IsGroupOwnerAsync(chatId, ownerId.Value);
                if (!isOwner)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "فقط مالک گروه می‌تواند آن را حذف کند.");
                    return;
                }

                // اطلاع‌رسانی به همه اعضای گروه قبل از حذف
                await Clients.Group(chatId.ToString()).SendAsync("GroupDeleted", chatId);

                // حذف گروه
                await _chatService.DeleteGroupAsync(chatId);

                _logger.LogInformation("User {OwnerId} deleted group {ChatId}", ownerId.Value, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {ChatId} by owner {OwnerId}", chatId, ownerId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در حذف گروه.");
            }
        }

        private async Task NotifyUserStatusChanged(long userId, bool isOnline)
        {
            try
            {
                // دریافت چت‌های خصوصی کاربر
                var userChats = await _chatService.GetUserChatsAsync(userId);
                var privateChats = userChats.Where(c => !c.IsGroup).ToList();

                // استخراج آیدی کاربران دیگر در چت‌های خصوصی
                var contactIds = new HashSet<long>();
                foreach (var chat in privateChats)
                {
                    foreach (var participant in chat.Participants)
                    {
                        if (participant.Id != userId)
                        {
                            contactIds.Add(participant.Id);
                        }
                    }
                }

                // ارسال وضعیت آنلاین بودن به هر کاربر
                foreach (var contactId in contactIds)
                {
                    var connectionIds = await _userConnectionService.GetConnectionsForUserAsync(contactId);

                    foreach (var connectionId in connectionIds)
                    {
                        await Clients.Client(connectionId).SendAsync(
                            "UserStatusChanged",
                            userId,
                            isOnline,
                            isOnline ? null : DateTime.UtcNow
                        );
                    }
                }

                _logger.LogDebug("User status changed notification sent for user {UserId} (online: {IsOnline}) to {ContactCount} contacts",
                    userId, isOnline, contactIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying user status change for user {UserId}", userId);
            }
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
    }
}