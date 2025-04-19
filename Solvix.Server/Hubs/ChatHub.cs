using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Solvix.Server.Data;
using Solvix.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;

namespace Solvix.Server.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatHub> _logger;
        private readonly IChatService _chatService;

        public ChatHub(IUserConnectionService userConnectionService, ILogger<ChatHub> logger, IChatService chatService /*, ChatDbContext context, UserManager<AppUser> userManager */)
        {
            _userConnectionService = userConnectionService;
            _logger = logger;
            _chatService = chatService;

        }

        private long? GetUserIdFromContext()
        {
            var userIdString = Context.UserIdentifier;
            if (long.TryParse(userIdString, out var uid))
            {
                return uid;
            }
            _logger.LogWarning("Could not parse UserIdentifier '{UserIdentifier}' to long for connection {ConnectionId}.", userIdString, Context.ConnectionId);
            return null;
        }


        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                await _userConnectionService.AddConnection(userId.Value, Context.ConnectionId);
                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId.Value, Context.ConnectionId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var connectionId = Context.ConnectionId;
            var userId = await _userConnectionService.GetUserIdForConnection(connectionId);
            await _userConnectionService.RemoveConnection(connectionId);
            if (userId.HasValue)
            {
                _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}. Error: {Error}", userId.Value, connectionId, ex?.Message);
            }
            else
            {
                _logger.LogWarning("Connection {ConnectionId} disconnected without a mapped user. Error: {Error}", connectionId, ex?.Message);
            }

            await base.OnDisconnectedAsync(ex);
        }

        public async Task SendToChat(Guid chatId, string messageContent)
        {
            var senderUserId = GetUserIdFromContext();
            if (!senderUserId.HasValue)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Authentication error. Cannot send message.");
                return;
            }

            try
            {
                var savedMessage = await _chatService.SaveMessageAsync(chatId, senderUserId.Value, messageContent);

                await _chatService.BroadcastMessageAsync(savedMessage);


                // کلاینت فرستنده ممکن است نیاز به تاییدیه یا جزئیات پیام ذخیره شده داشته باشد.
                // await Clients.Caller.SendAsync("MessageSentConfirmation", savedMessage.Id);

            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("User {UserId} sent invalid message content to Chat {ChatId}. Error: {Error}", senderUserId.Value, chatId, ex.Message);
                await Clients.Caller.SendAsync("ReceiveError", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant. Error: {Error}", senderUserId.Value, chatId, ex.Message);
                await Clients.Caller.SendAsync("ReceiveError", "You are not a member of this chat.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ChatService.SaveMessageAsync or BroadcastMessageAsync for Chat {ChatId} by User {UserId}.", chatId, senderUserId.Value);
                await Clients.Caller.SendAsync("ReceiveError", "An unexpected error occurred while sending your message.");
            }
        }

        public async Task MarkMultipleMessagesAsRead(List<int> messageIds)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue) return;

            try
            {
                foreach (var messageId in messageIds)
                {
                    await _chatService.MarkMessageAsReadAsync(messageId, userId.Value);
                }
                _logger.LogInformation("User {UserId} marked {Count} messages as read", userId.Value, messageIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ChatService.MarkMessageAsReadAsync for multiple messages by User {UserId}.", userId.Value);
            }
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            var readerUserId = GetUserIdFromContext();
            if (!readerUserId.HasValue) return;

            try
            {
                await _chatService.MarkMessageAsReadAsync(messageId, readerUserId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ChatService.MarkMessageAsReadAsync for Message {MessageId} by User {UserId}.", messageId, readerUserId.Value);
                // در صورت لزوم میتوانید یک خطا به کلاینت بفرستید
                // await Clients.Caller.SendAsync("ReceiveError", "Error marking message as read.");
            }
        }
    }
}