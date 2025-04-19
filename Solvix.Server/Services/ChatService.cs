using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Solvix.Server.Data;
using Solvix.Server.Hubs;
using Solvix.Server.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Solvix.Server.Services
{
    public class ChatService : IChatService
    {
        private readonly ChatDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(ChatDbContext context, IHubContext<ChatHub> hubContext, IUserConnectionService userConnectionService, ILogger<ChatService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _userConnectionService = userConnectionService;
            _logger = logger;
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
                _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant.", senderId, chatId);
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

            _context.Messages.Add(newMessage);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Message {MessageId} saved for Chat {ChatId} by User {UserId}", newMessage.Id, chatId, senderId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save message to database for Chat {ChatId} by User {UserId}.", chatId, senderId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while saving message for Chat {ChatId} by User {UserId}.", chatId, senderId);
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
                var sender = await _context.Users.FindAsync(message.SenderId);
                if (sender == null)
                {
                    _logger.LogError("Cannot broadcast message {MessageId}. Sender user with ID {UserId} not found.", message.Id, message.SenderId);
                    return;
                }

                var senderFullName = $"{sender.FirstName} {sender.LastName}".Trim();

                var chatParticipants = await _context.ChatParticipants
                    .Where(cp => cp.ChatId == message.ChatId)
                    .Select(cp => cp.UserId)
                    .ToListAsync();

                foreach (var participantUserId in chatParticipants)
                {
                    var connectionIds = await _userConnectionService.GetConnectionsForUser(participantUserId);

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
                                _logger.LogInformation("Sent message {MessageId} to User {UserId} on connection {ConnectionId}", message.Id, participantUserId, connectionId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending message {MessageId} to User {UserId} on connection {ConnectionId}", message.Id, participantUserId, connectionId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while broadcasting message {MessageId} for Chat {ChatId}.", message.Id, message.ChatId);
                throw;
            }
        }

        public async Task MarkMessageAsReadAsync(int messageId, long readerUserId)
        {
            var message = await _context.Messages
                                 .Include(m => m.Chat)
                                 .ThenInclude(c => c.Participants)
                                 .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.SenderId == readerUserId || message.IsRead)
            {
                return;
            }

            if (!message.Chat.Participants.Any(p => p.UserId == readerUserId))
            {
                _logger.LogWarning("User {UserId} tried to mark message {MessageId} as read in chat {ChatId} without being a participant.", readerUserId, messageId, message.ChatId);
                return;
            }

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Message {MessageId} marked as read by User {UserId}", messageId, readerUserId);

                var senderConnectionIds = await _userConnectionService.GetConnectionsForUser(message.SenderId);
                foreach (var connectionId in senderConnectionIds)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync(
                            "MessageRead",
                            message.ChatId,
                            message.Id
                        );
                        _logger.LogInformation("Notified sender {SenderId} on connection {ConnectionId} that message {MessageId} was read.", message.SenderId, connectionId, messageId);
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to mark message {MessageId} as read by User {UserId}.", messageId, readerUserId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while marking message {MessageId} as read by User {UserId}.", messageId, readerUserId);
                throw;
            }
        }

        // پیاده سازی متد کمکی چک کردن شرکت کننده بودن کاربر (اختیاری)
        // این منطق در BaseController هم وجود دارد، میتوانید انتخاب کنید کجا از آن استفاده کنید.
        // اگر منطق چک کردن شرکت کننده بودن قبل از عملیات حساس در سرویس لازم است، پیاده سازی آن در سرویس هم منطقی است.
        public async Task<bool> IsUserParticipantAsync(Guid chatId, long userId)
        {
            return await _context.ChatParticipants
                .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);
        }


        public async Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId)
        {
            if (messageIds == null || !messageIds.Any())
                return;

            var messages = await _context.Messages
                .Include(m => m.Chat)
                .ThenInclude(c => c.Participants)
                .Where(m => messageIds.Contains(m.Id) && m.SenderId != readerUserId && !m.IsRead)
                .ToListAsync();

            if (!messages.Any())
                return;

            // Group by ChatId to check permissions once per chat
            var messagesByChat = messages.GroupBy(m => m.ChatId);

            foreach (var chatGroup in messagesByChat)
            {
                var chatId = chatGroup.Key;
                var firstMessage = chatGroup.First();

                // Check if user is participant in this chat
                if (!firstMessage.Chat.Participants.Any(p => p.UserId == readerUserId))
                {
                    _logger.LogWarning("User {UserId} tried to mark messages as read in chat {ChatId} without being a participant.", readerUserId, chatId);
                    continue;
                }

                // Mark all messages in this chat as read
                foreach (var message in chatGroup)
                {
                    message.IsRead = true;
                    message.ReadAt = DateTime.UtcNow;
                }
            }

            // Save all changes at once
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("{Count} messages marked as read by User {UserId}", messages.Count, readerUserId);

                // Notify senders about read messages
                var messagesBySender = messages.GroupBy(m => m.SenderId);

                foreach (var senderGroup in messagesBySender)
                {
                    var senderId = senderGroup.Key;
                    var senderConnections = await _userConnectionService.GetConnectionsForUser(senderId);

                    foreach (var connectionId in senderConnections.Where(c => !string.IsNullOrEmpty(c)))
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
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to mark messages as read by User {UserId}.", readerUserId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while marking messages as read by User {UserId}.", readerUserId);
                throw;
            }
        }
    }
}