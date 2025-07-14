using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Core.Entities;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.API.Hubs;
using Microsoft.VisualBasic;
using FirebaseAdmin.Messaging;
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

                if (!await IsUserParticipantAsync(chatId, userId))
                {
                    _logger.LogWarning("User {UserId} is not a participant of chat {ChatId}.", userId, chatId);
                    return null;
                }

                var participantIds = chat.Participants.Select(p => p.UserId).Distinct().ToList();
                var onlineStatuses = new Dictionary<long, bool>();
                foreach (var id in participantIds)
                {
                    onlineStatuses[id] = await _userConnectionService.IsUserOnlineAsync(id);
                }

                var chatDto = MappingHelper.MapToChatDto(chat, userId, onlineStatuses);
                _logger.LogInformation("Returning ChatDto for chat {ChatId}", chatId);
                return chatDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat {ChatId} for user {UserId}", chatId, userId);
                return null;
            }
        }

        public async Task<(Guid chatId, bool alreadyExists)> StartChatWithUserAsync(long initiatorUserId, long recipientUserId)
        {
            if (initiatorUserId == recipientUserId) throw new InvalidOperationException("Cannot start a chat with yourself");
            try
            {
                var existingChat = await _unitOfWork.ChatRepository.GetPrivateChatBetweenUsersAsync(initiatorUserId, recipientUserId);
                if (existingChat != null) return (existingChat.Id, true);

                var chat = new Chat { IsGroup = false, CreatedAt = DateTime.UtcNow };
                await _unitOfWork.ChatRepository.AddAsync(chat);
                await _unitOfWork.ChatRepository.AddParticipantAsync(chat.Id, initiatorUserId);
                await _unitOfWork.ChatRepository.AddParticipantAsync(chat.Id, recipientUserId);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("New chat created between {User1} and {User2}. ChatId: {ChatId}", initiatorUserId, recipientUserId, chat.Id);
                return (chat.Id, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat between {User1} and {User2}", initiatorUserId, recipientUserId); throw;
            }
        }

        public async Task<SolvixMessage> SaveMessageAsync(Guid chatId, long senderId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Message content cannot be empty.");

            var isParticipant = await IsUserParticipantAsync(chatId, senderId);
            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant.", senderId, chatId);
                throw new UnauthorizedAccessException("User is not a participant of this chat.");
            }

            var newMessage = new SolvixMessage
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                ReadAt = null
            };

            await _unitOfWork.MessageRepository.AddAsync(newMessage);

            var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId);
            if (chat != null)
            {
                chat.LastMessage = content;
                chat.LastMessageTime = newMessage.SentAt;
                // منطق UnreadCount را هم باید اینجا پیاده کنید
            }

            try
            {
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Message {MessageId} saved for Chat {ChatId} by User {UserId}", newMessage.Id, chatId, senderId);
                return newMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save message to database for Chat {ChatId} by User {UserId}.", chatId, senderId);
                throw;
            }
        }

        public async Task BroadcastMessageAsync(SolvixMessage message)
        {
            if (message == null || message.Id <= 0)
            {
                _logger.LogError("BroadcastMessageAsync called with invalid message.");
                return;
            }

            try
            {
                if (message.Sender == null)
                {
                    message.Sender = await _unitOfWork.UserRepository.GetByIdAsync(message.SenderId);
                    if (message.Sender == null)
                    {
                        _logger.LogError("Cannot broadcast. Sender with ID {SenderId} not found.", message.SenderId);
                        return;
                    }
                }
                var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(message.ChatId);
                if (chat == null)
                {
                    _logger.LogError("Cannot broadcast. Chat {ChatId} not found.", message.ChatId);
                    return;
                }
                var messageDto = MappingHelper.MapToMessageDto(message);
                var participantIds = chat.Participants.Select(p => p.UserId).ToList();

                foreach (var participantId in participantIds)
                {
                    var connectionIds = await _userConnectionService.GetConnectionsForUserAsync(participantId);

                    if (connectionIds.Any())
                    {
                        foreach (var connectionId in connectionIds)
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMessage", messageDto);
                        }
                    }
                    else if (participantId != message.SenderId)
                    {
                        var recipientUser = await _unitOfWork.UserRepository.GetByIdAsync(participantId);
                        if (recipientUser != null && !string.IsNullOrEmpty(recipientUser.FcmToken))
                        {
                            _logger.LogInformation("FCM token found for user {UserId}. Preparing to send notification.", recipientUser.Id);

                            var sender = message.Sender;
                            var notifTitle = $"{sender?.FirstName} {sender?.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(notifTitle))
                            {
                                notifTitle = sender?.UserName ?? "پیام جدید";
                            }

                            var notificationPayload = new Notification
                            {
                                Title = notifTitle,
                                Body = message.Content
                            };

                            if (!string.IsNullOrEmpty(sender?.ProfilePictureUrl))
                            {
                                notificationPayload.ImageUrl = sender.ProfilePictureUrl;
                            }

                            var notifData = new Dictionary<string, string>
                            {
                                { "chatId", message.ChatId.ToString() },
                                { "type", "new_message" },
                                { "senderId", sender.Id.ToString() },
                                { "senderName", notifTitle },
                                { "profilePictureUrl", sender?.ProfilePictureUrl ?? "" }
                            };

                            await _notificationService.SendNotificationAsync(recipientUser, notificationPayload, notifData);
                        }
                        else
                        {
                            _logger.LogWarning("FCM token NOT found for offline user {UserId}. Skipping notification.", participantId);
                        }
                    }
                }
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

                var messageDtos = messages.Select(MappingHelper.MapToMessageDto).ToList();
                _logger.LogInformation("Returning {Count} messages for chat {ChatId}", messageDtos.Count, chatId);
                return messageDtos;
            }
            catch (UnauthorizedAccessException) { throw; }
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
                    _logger.LogWarning("MarkMessageAsReadAsync: Message {MessageId} not found or sender is the reader {UserId}.", messageId, readerUserId);
                    return;
                }

                if (!await IsUserParticipantAsync(message.ChatId, readerUserId))
                {
                    _logger.LogWarning("MarkMessageAsReadAsync: User {UserId} is not a participant of chat {ChatId}.", readerUserId, message.ChatId);
                    return;
                }

                await _unitOfWork.MessageRepository.MarkAsReadAsync(messageId, readerUserId);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Message {MessageId} marked as read by user {UserId}.", messageId, readerUserId);

                var senderConnections = await _userConnectionService.GetConnectionsForUserAsync(message.SenderId);
                foreach (var connectionId in senderConnections)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync(
                            "MessageStatusChanged",
                            message.ChatId,
                            messageId,
                            Constants.MessageStatus.Read
                        );
                        _logger.LogDebug("Sent MessageRead notification for MessageId {MessageId} to Sender {SenderId} on connection {ConnectionId}", messageId, message.SenderId, connectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read by user {UserId}", messageId, readerUserId);
            }
        }

        public async Task<SolvixMessage?> EditMessageAsync(int messageId, string newContent, long editorUserId)
        {
            var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
            if (message == null || message.SenderId != editorUserId || message.IsDeleted)
            {
                return null;
            }

            message.Content = newContent;
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;

            await _unitOfWork.CompleteAsync();
            _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, editorUserId);
            return message;
        }

        public async Task<SolvixMessage?> DeleteMessageAsync(int messageId, long deleterUserId)
        {
            var message = await _unitOfWork.MessageRepository.GetByIdAsync(messageId);
            if (message == null || message.SenderId != deleterUserId || message.IsDeleted)
            {
                return null;
            }

            message.IsDeleted = true;

            await _unitOfWork.CompleteAsync();
            _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, deleterUserId);
            return message;
        }

        public async Task BroadcastMessageUpdateAsync(SolvixMessage message)
        {
            var chat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(message.ChatId);
            if (chat == null) return;

            var messageDto = MappingHelper.MapToMessageDto(message);
            var participantIds = chat.Participants.Select(p => p.UserId).ToList();

            foreach (var userId in participantIds)
            {
                var connectionIds = await _userConnectionService.GetConnectionsForUserAsync(userId);
                foreach (var connectionId in connectionIds)
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("MessageUpdated", messageDto);
                }
            }
        }

        public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId)
        {
            if (messageIds == null || !messageIds.Any()) return;

            try
            {
                var messages = await _unitOfWork.MessageRepository.ListAsync(m => messageIds.Contains(m.Id));
                if (!messages.Any()) return;

                var validMessageIds = messages
                    .Where(m => m.SenderId != readerUserId && !m.IsRead)
                    .Select(m => m.Id)
                    .ToList();

                if (!validMessageIds.Any())
                {
                    _logger.LogInformation("MarkMultipleMessagesAsReadAsync: No valid messages found to mark as read for user {UserId}.", readerUserId);
                    return;
                }

                var chatId = messages.First().ChatId;
                if (!await IsUserParticipantAsync(chatId, readerUserId))
                {
                    _logger.LogWarning("MarkMultipleMessagesAsReadAsync: User {UserId} is not a participant of chat {ChatId}.", readerUserId, chatId);
                    return;
                }

                await _unitOfWork.MessageRepository.MarkMultipleAsReadAsync(validMessageIds, readerUserId);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Marked {Count} messages as read by user {UserId} in chat {ChatId}.", validMessageIds.Count, readerUserId, chatId);

                var senderGroups = messages
                    .Where(m => validMessageIds.Contains(m.Id))
                    .GroupBy(m => m.SenderId);

                foreach (var group in senderGroups)
                {
                    var senderId = group.Key;
                    var senderConnections = await _userConnectionService.GetConnectionsForUserAsync(senderId);
                    var readMessageIdsInGroup = group.Select(m => m.Id).ToList();

                    foreach (var connectionId in senderConnections)
                    {
                        if (!string.IsNullOrEmpty(connectionId))
                        {
                            foreach (var messageId in readMessageIdsInGroup)
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync(
                                    "MessageStatusChanged",
                                    chatId,
                                    messageId,
                                    Constants.MessageStatus.Read
                                );
                            }
                            _logger.LogDebug("Sent MessageRead notifications for {Count} messages to Sender {SenderId} on connection {ConnectionId}", readMessageIdsInGroup.Count, senderId, connectionId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking multiple messages as read by user {UserId}", readerUserId);
            }
        }


        public async Task<ChatDto> CreateGroupChatAsync(long creatorId, string title, List<long> participantIds)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Group title cannot be empty.");
            if (participantIds == null || !participantIds.Any()) throw new ArgumentException("Group must have participants.");

            // افزودن سازنده گروه به لیست اعضا اگر وجود نداشت
            if (!participantIds.Contains(creatorId))
            {
                participantIds.Add(creatorId);
            }

            var chat = new Chat
            {
                IsGroup = true,
                Title = title,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ChatRepository.AddAsync(chat);

            foreach (var userId in participantIds)
            {
                await _unitOfWork.ChatRepository.AddParticipantAsync(chat.Id, userId);
            }

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("New group chat created by {CreatorId} with title '{Title}'. ChatId: {ChatId}", creatorId, title, chat.Id);

            // واکشی اطلاعات کامل برای DTO
            var createdChat = await _unitOfWork.ChatRepository.GetChatWithParticipantsAsync(chat.Id);
            var onlineStatuses = new Dictionary<long, bool>();
            foreach (var p in createdChat.Participants)
            {
                onlineStatuses[p.UserId] = await _userConnectionService.IsUserOnlineAsync(p.UserId);
            }

            return MappingHelper.MapToChatDto(createdChat, creatorId, onlineStatuses);
        }

        public async Task<bool> IsUserParticipantAsync(Guid chatId, long userId)
        {
            return await _unitOfWork.ChatRepository.IsUserParticipantAsync(chatId, userId);
        }
    }
}