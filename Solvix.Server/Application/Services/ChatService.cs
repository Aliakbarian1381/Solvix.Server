using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Core.Entities;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.API.Hubs;

namespace Solvix.Server.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IUnitOfWork unitOfWork,
            IHubContext<ChatHub> hubContext,
            IUserConnectionService userConnectionService,
            ILogger<ChatService> logger)
        {
            _unitOfWork = unitOfWork;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
            _logger = logger;
        }

        public async Task<List<ChatDto>> GetUserChatsAsync(long userId)
        {
            try
            {
                var chats = await _unitOfWork.ChatRepository.GetUserChatsAsync(userId);
                return chats.Select(chat => MappingHelper.MapToChatDto(chat, userId)).ToList();
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
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chatId);

                if (chat == null)
                    return null;

                // بررسی آیا کاربر عضو چت است
                if (!await IsUserParticipantAsync(chatId, userId))
                    return null;

                return MappingHelper.MapToChatDto(chat, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat {ChatId} for user {UserId}", chatId, userId);
                return null;
            }
        }

        public async Task<(Guid chatId, bool alreadyExists)> StartChatWithUserAsync(long initiatorUserId, long recipientUserId)
        {
            if (initiatorUserId == recipientUserId)
            {
                throw new InvalidOperationException("Cannot start a chat with yourself");
            }

            try
            {
                // بررسی آیا چت خصوصی قبلاً وجود دارد
                var existingChat = await _unitOfWork.ChatRepository.GetPrivateChatBetweenUsersAsync(initiatorUserId, recipientUserId);

                if (existingChat != null)
                {
                    return (existingChat.Id, true);
                }

                // ساخت چت جدید
                var chat = new Chat
                {
                    IsGroup = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.ChatRepository.AddAsync(chat);

                // افزودن شرکت‌کنندگان
                await _unitOfWork.ChatRepository.AddParticipantAsync(chat.Id, initiatorUserId);
                await _unitOfWork.ChatRepository.AddParticipantAsync(chat.Id, recipientUserId);

                await _unitOfWork.CompleteAsync();

                return (chat.Id, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat between users {InitiatorId} and {RecipientId}",
                    initiatorUserId, recipientUserId);
                throw;
            }
        }

        public async Task<Message> SaveMessageAsync(Guid chatId, long senderId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Message content cannot be empty.");
            }

            var isParticipant = await IsUserParticipantAsync(chatId, senderId);
            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant.",
                    senderId, chatId);
                throw new UnauthorizedAccessException("User is not a participant of this chat.");
            }

            var newMessage = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            await _unitOfWork.MessageRepository.AddAsync(newMessage);

            try
            {
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Message {MessageId} saved for Chat {ChatId} by User {UserId}",
                    newMessage.Id, chatId, senderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save message to database for Chat {ChatId} by User {UserId}.",
                    chatId, senderId);
                throw;
            }

            return newMessage;
        }

        public async Task BroadcastMessageAsync(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                // دریافت اطلاعات فرستنده
                var sender = await _unitOfWork.UserRepository.GetByIdAsync(message.SenderId);
                if (sender == null)
                {
                    _logger.LogError("Cannot broadcast message {MessageId}. Sender user with ID {UserId} not found.",
                        message.Id, message.SenderId);
                    return;
                }

                var senderFullName = $"{sender.FirstName} {sender.LastName}".Trim();

                // دریافت تمام شرکت‌کنندگان در چت
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(message.ChatId);
                if (chat == null)
                {
                    _logger.LogError("Cannot broadcast message {MessageId}. Chat {ChatId} not found.",
                        message.Id, message.ChatId);
                    return;
                }

                var participantIds = chat.Participants.Select(p => p.UserId).ToList();

                // ارسال پیام به همه شرکت‌کنندگان آنلاین
                foreach (var participantId in participantIds)
                {
                    var connectionIds = await _userConnectionService.GetConnectionsForUserAsync(participantId);

                    foreach (var connectionId in connectionIds)
                    {
                        if (!string.IsNullOrEmpty(connectionId))
                        {
                            try
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync(
                                    "ReceiveMessage",
                                    message.Id,
                                    message.SenderId,
                                    senderFullName,
                                    message.Content,
                                    message.ChatId,
                                    message.SentAt
                                );
                                _logger.LogInformation("Sent message {MessageId} to User {UserId} on connection {ConnectionId}",
                                    message.Id, participantId, connectionId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending message {MessageId} to User {UserId} on connection {ConnectionId}",
                                    message.Id, participantId, connectionId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while broadcasting message {MessageId} for Chat {ChatId}.",
                    message.Id, message.ChatId);
                throw;
            }
        }

        public async Task<List<MessageDto>> GetChatMessagesAsync(Guid chatId, long userId, int skip = 0, int take = 50)
        {
            try
            {
                // بررسی آیا کاربر عضو چت است
                if (!await IsUserParticipantAsync(chatId, userId))
                {
                    _logger.LogWarning("User {UserId} attempted to get messages for Chat {ChatId} without being a participant.",
                        userId, chatId);
                    throw new UnauthorizedAccessException("User is not a participant of this chat.");
                }

                var messages = await _unitOfWork.MessageRepository.GetChatMessagesAsync(chatId, skip, take);

                // بروزرسانی وضعیت خواندن پیام‌ها
                var unreadMessages = messages.Where(m => m.SenderId != userId && !m.IsRead).ToList();
                foreach (var message in unreadMessages)
                {
                    await MarkMessageAsReadAsync(message.Id, userId);
                }

                await _unitOfWork.CompleteAsync();

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
                await _unitOfWork.MessageRepository.MarkAsReadAsync(messageId, readerUserId);
                await _unitOfWork.CompleteAsync();

                // دریافت پیام برای اطلاع‌رسانی به فرستنده
                var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
                if (message != null && message.SenderId != readerUserId)
                {
                    // ارسال نوتیفیکیشن به فرستنده
                    var senderConnections = await _userConnectionService.GetConnectionsForUserAsync(message.SenderId);
                    foreach (var connectionId in senderConnections)
                    {
                        if (!string.IsNullOrEmpty(connectionId))
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync(
                                "MessageRead",
                                message.ChatId,
                                messageId
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read by user {UserId}", messageId, readerUserId);
            }
        }

        public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId)
        {
            if (messageIds == null || !messageIds.Any())
                return;

            try
            {
                await _unitOfWork.MessageRepository.MarkMultipleAsReadAsync(messageIds, readerUserId);
                await _unitOfWork.CompleteAsync();

                // گروه‌بندی پیام‌ها بر اساس فرستنده
                var messages = await _unitOfWork.MessageRepository.ListAsync(m => messageIds.Contains(m.Id));
                var messagesBySender = messages
                    .Where(m => m.SenderId != readerUserId)
                    .GroupBy(m => m.SenderId);

                foreach (var senderGroup in messagesBySender)
                {
                    var senderId = senderGroup.Key;
                    var senderConnections = await _userConnectionService.GetConnectionsForUserAsync(senderId);

                    foreach (var connectionId in senderConnections)
                    {
                        if (!string.IsNullOrEmpty(connectionId))
                        {
                            foreach (var message in senderGroup)
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync(
                                    "MessageRead",
                                    message.ChatId,
                                    message.Id
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple messages as read by user {UserId}", readerUserId);
            }
        }

        public async Task<bool> IsUserParticipantAsync(Guid chatId, long userId)
        {
            return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);
        }
    }
}