using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Solvix.Server.Core.Interfaces;
using System.Security.Claims;

namespace Solvix.Server.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IUserConnectionService _connectionService;
        private readonly IChatService _chatService;
        private readonly IUserService _userService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            IUserConnectionService connectionService,
            IChatService chatService,
            IUserService userService,
            ILogger<ChatHub> logger)
        {
            _connectionService = connectionService;
            _chatService = chatService;
            _userService = userService;
            _logger = logger;
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
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                await _connectionService.AddConnectionAsync(userId.Value, Context.ConnectionId);
                await _userService.UpdateUserLastActiveAsync(userId.Value);

                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}",
                    userId.Value, Context.ConnectionId);

                await Clients.Caller.SendAsync("HubConnectionRegistered");
                _logger.LogInformation("Sent HubConnectionRegistered to client {ConnectionId}", Context.ConnectionId);

                // اطلاع‌رسانی به کاربران دیگر درباره آنلاین شدن این کاربر
                await NotifyUserStatusChanged(userId.Value, true);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var userId = await _connectionService.GetUserIdForConnectionAsync(connectionId);

            await _connectionService.RemoveConnectionAsync(connectionId);

            if (userId.HasValue)
            {
                await _userService.UpdateUserLastActiveAsync(userId.Value);

                _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}. Error: {Error}",
                    userId.Value, connectionId, exception?.Message);

                // بررسی آیا کاربر هنوز اتصال دیگری دارد
                var remainingConnections = await _connectionService.GetConnectionsForUserAsync(userId.Value);
                if (remainingConnections.Count == 0)
                {
                    // اطلاع‌رسانی به کاربران دیگر درباره آفلاین شدن کاربر
                    await NotifyUserStatusChanged(userId.Value, false);
                }
            }
            else
            {
                _logger.LogWarning("Connection {ConnectionId} disconnected without a mapped user. Error: {Error}",
                    connectionId, exception?.Message);
            }

            await base.OnDisconnectedAsync(exception);
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
                correlationId = Guid.NewGuid().ToString("N"); // ایجاد correlationId اگر ارائه نشده باشد
            }

            try
            {
                // ذخیره پیام و دریافت نسخه ذخیره شده
                var savedMessage = await _chatService.SaveMessageAsync(chatId, senderUserId.Value, messageContent);

                // ارسال تأییدیه همبستگی به فرستنده
                await Clients.Caller.SendAsync("MessageCorrelationConfirmation", correlationId, savedMessage.Id);

                // انتشار پیام به همه اعضای چت
                await _chatService.BroadcastMessageAsync(savedMessage);

                // لاگ موفقیت
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
                _logger.LogError(ex, "Error sending message to Chat {ChatId} by User {UserId} with correlation {CorrelationId}.",
                    chatId, senderUserId.Value, correlationId);
                await Clients.Caller.SendAsync("ReceiveError", "خطا در ارسال پیام. لطفاً دوباره تلاش کنید.");
            }
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            var readerUserId = GetUserIdFromContext();
            if (!readerUserId.HasValue)
                return;

            try
            {
                await _chatService.MarkMessageAsReadAsync(messageId, readerUserId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read by User {UserId}.",
                    messageId, readerUserId.Value);
            }
        }

        public async Task MarkMultipleMessagesAsRead(List<int> messageIds)
        {
            var readerUserId = GetUserIdFromContext();
            if (!readerUserId.HasValue || messageIds == null || !messageIds.Any())
                return;

            try
            {
                await _chatService.MarkMultipleMessagesAsReadAsync(messageIds, readerUserId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple messages as read by User {UserId}.",
                    readerUserId.Value);
            }
        }


        public async Task UserTyping(Guid chatId, bool isTyping)
        {
            var typingUserId = GetUserIdFromContext();
            if (!typingUserId.HasValue)
                return;

            try
            {
                // Verify user is a participant of the chat
                if (await _chatService.IsUserParticipantAsync(chatId, typingUserId.Value))
                {
                    // Get all other participants in the chat
                    var chat = await _chatService.GetChatByIdAsync(chatId, typingUserId.Value);
                    if (chat == null) return;

                    foreach (var participant in chat.Participants.Where(p => p.Id != typingUserId.Value))
                    {
                        var connections = await _connectionService.GetConnectionsForUserAsync(participant.Id);
                        foreach (var connectionId in connections)
                        {
                            await Clients.Client(connectionId).SendAsync(
                                "UserTyping",
                                chatId,
                                typingUserId.Value,
                                isTyping
                            );
                        }
                    }

                    _logger.LogDebug("User {UserId} typing status ({IsTyping}) sent to participants of chat {ChatId}",
                        typingUserId.Value, isTyping, chatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting typing status for User {UserId} in chat {ChatId}",
                    typingUserId.Value, chatId);
            }
        }

        private async Task NotifyUserStatusChanged(long userId, bool isOnline)
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
                var connectionIds = await _connectionService.GetConnectionsForUserAsync(contactId);

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
        }
    }
}