using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Infrastructure.Repositories;
using System.Security.Claims;

namespace Solvix.Server.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IUserConnectionService _connectionService;
        private readonly IChatService _chatService;
        private readonly IUserService _userService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            IUserConnectionService connectionService,
            IChatService chatService,
            IUserService userService,
            IUnitOfWork unitOfWork,
            ILogger<ChatHub> logger)
        {
            _connectionService = connectionService;
            _chatService = chatService;
            _userService = userService;
            _unitOfWork = unitOfWork;
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
                _logger.LogError("User ID not found in token claims");
                await Clients.Caller.SendAsync("ReceiveError", "خطا در احراز هویت. امکان ارسال پیام وجود ندارد.");
                return;
            }

            try
            {
                _logger.LogInformation("Received message from user {UserId} to chat {ChatId}: {Content}",
                    senderUserId.Value, chatId, messageContent.Substring(0, Math.Min(20, messageContent.Length)));

                // بررسی عضویت کاربر در چت
                if (!await _chatService.IsUserParticipantAsync(chatId, senderUserId.Value))
                {
                    _logger.LogWarning("User {UserId} is not a participant of chat {ChatId}", senderUserId.Value, chatId);
                    await Clients.Caller.SendAsync("ReceiveError", "شما عضو این چت نیستید.");
                    return;
                }

                // ذخیره پیام در دیتابیس
                var savedMessage = await _chatService.SaveMessageAsync(chatId, senderUserId.Value, messageContent);

                // ارسال تأییدیه به فرستنده
                await Clients.Caller.SendAsync("MessageCorrelationConfirmation", correlationId, savedMessage.Id);

                // انتشار پیام به همه شرکت‌کنندگان در چت
                await _chatService.BroadcastMessageAsync(savedMessage);

                _logger.LogInformation("Message saved and broadcast successfully. ID: {MessageId}", savedMessage.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendToChat for chat {ChatId}", chatId);
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
                var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                if (message == null || message.SenderId == readerUserId.Value)
                {
                    return;
                }

                if (!await _chatService.IsUserParticipantAsync(message.ChatId, readerUserId.Value))
                {
                    return;
                }

                await _unitOfWork.MessageRepository.MarkAsReadAsync(messageId, readerUserId.Value);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Message {MessageId} marked as read by user {UserId}", messageId, readerUserId.Value);

                var senderConnections = await _connectionService.GetConnectionsForUserAsync(message.SenderId);
                foreach (var connectionId in senderConnections)
                {
                    await Clients.Client(connectionId).SendAsync(
                        "MessageStatusChanged",
                        message.ChatId,
                        messageId,
                        Constants.MessageStatus.Read
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
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