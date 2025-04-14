using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Services;
using Microsoft.AspNetCore.Authorization;


namespace Solvix.Server.Hubs
{
    [Authorize] /*فقط کاربران احراز هویت شده می‌توانند وصل شوند*/
    public class ChatHub : Hub
    {
        private readonly IUserService _userService;
        private readonly IMessageService _messageService;
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger, IUserService userService, IMessageService messageService, IUserConnectionService userConnectionService)
        {
            _logger = logger;
            _userService = userService;
            _messageService = messageService;
            _userConnectionService = userConnectionService;
        }



        public override async Task OnConnectedAsync()
        {
            var userIdString = Context.UserIdentifier;
            var username = Context.User?.Identity?.Name ?? "Unknown";

            if (string.IsNullOrEmpty(userIdString) || !long.TryParse(userIdString, out var userId))
            {
                Context.Abort(); 
                await base.OnConnectedAsync();
                return;
            }

            try
            {
                await _userConnectionService.AddConnection(userId, Context.ConnectionId);
                await SendConnectedUsers();

                // TODO: ارسال پیام‌های آفلاین برای این کاربر (اگر لازم است)
                // var unreadMessages = await _messageService.GetUnreadMessagesForUser(userId);
                // await Clients.Caller.SendAsync("ReceiveOfflineMessages", unreadMessages); // نام متد و داده‌ها مثال است

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during OnConnectedAsync for User ID: {userId}, ConnectionId: {Context.ConnectionId}");
                Context.Abort(); // در صورت بروز خطا، اتصال را قطع کن
            }
            finally
            {
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            long? userId = null;

            try
            {
                userId = await _userConnectionService.GetUserIdForConnection(connectionId);

                await _userConnectionService.RemoveConnection(connectionId);

                if (userId != null)
                {
                    await SendConnectedUsers();
                }
                else
                {
                    _logger.LogWarning($"Disconnected connection without associated user: ConnectionId={connectionId}, Reason: {exception?.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during OnDisconnectedAsync for ConnectionId: {connectionId}, Associated User ID (if found): {userId}");
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }


        public async Task SendPrivateMessage(string recipientUsername, string message)
        {
            var senderIdString = Context.UserIdentifier;
            var senderUsername = Context.User?.Identity?.Name ?? "Unknown Sender";

            if (string.IsNullOrEmpty(senderIdString) || !long.TryParse(senderIdString, out var senderId))
            {
                await Clients.Caller.SendAsync("ErrorMessage", "خطای داخلی: فرستنده نامعتبر است.");
                return;
            }

            var recipientUser = await _userService.GetUserByUsername(recipientUsername);
            if (recipientUser == null)
            {
                await Clients.Caller.SendAsync("ErrorMessage", $"کاربر گیرنده '{recipientUsername}' یافت نشد.");
                return;
            }

            if (recipientUser.Id == senderId)
            {
                await Clients.Caller.SendAsync("ErrorMessage", "نمی‌توانید به خودتان پیام خصوصی بفرستید.");
                return;
            }

            try
            {
                await _messageService.SaveMessage(senderId, recipientUser.Id, message);

                var recipientConnections = await _userConnectionService.GetConnectionsForUser(recipientUser.Id);
                if (recipientConnections.Any())
                {
                    foreach (var connectionId in recipientConnections)
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveMessage", senderUsername, message); // ارسال نام کاربری فرستنده و پیام
                    }
                }
                else
                {
                    _logger.LogInformation($"Recipient {recipientUsername} is offline. Message saved from {senderUsername}.");
                }

                await Clients.Caller.SendAsync("ReceiveMessage", senderUsername, message);

            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ErrorMessage", "خطا در ارسال یا ذخیره پیام.");
            }
        }


        private async Task SendConnectedUsers()
        {
            var onlineUsers = await _userConnectionService.GetOnlineUsers();
            await Clients.All.SendAsync("ConnectedUsers", onlineUsers.Select(u => u.UserName).ToList());
        }
    }
}

