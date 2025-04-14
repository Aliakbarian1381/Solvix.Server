using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Services;
using System.Collections.Concurrent;


namespace Solvix.Server.Hubs
{
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
            var httpContext = Context.GetHttpContext();
            if (httpContext != null && httpContext.Request.Query.TryGetValue("user", out var username))
            {
                if (!string.IsNullOrEmpty(username))
                {
                    var user = await _userService.GetUserByUsername(username);
                    if (user != null)
                    {
                        // ثبت اتصال کاربر در دیتابیس
                        await _userConnectionService.AddConnection(user.Id, Context.ConnectionId);
                        await Clients.Caller.SendAsync("UserConnected", username);
                        await SendConnectedUsers();

                        // ارسال پیام‌های آفلاین
                        var unreadMessages = await _messageService.GetUnreadMessagesForUser(user.Id);
                        foreach (var message in unreadMessages)
                        {
                            await Clients.Caller.SendAsync("ReceiveMessage", message.Sender.Username, message.Content);
                            await _messageService.MarkMessageAsRead(message.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"User '{username}' not found.");
                        await Clients.Caller.SendAsync("ErrorMessage", "کاربری با این نام کاربری یافت نشد.");
                        Context.Abort();
                    }
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // حذف اتصال کاربر از دیتابیس
            await _userConnectionService.RemoveConnection(Context.ConnectionId);
            var userId = await _userConnectionService.GetUserIdForConnection(Context.ConnectionId);
            if (userId > 0)
            {
                var user = await _userService.GetUserById(userId);
                if (user != null)
                {
                    await Clients.All.SendAsync("UserDisconnected", user.Username);
                    await SendConnectedUsers();
                }
            }
            await base.OnDisconnectedAsync(exception);
        }


        public async Task SendPrivateMessage(string recipientUsername, string message)
        {
            var senderUsername = Context.UserIdentifier; // اگر از احراز هویت استفاده می‌کنید
            if (string.IsNullOrEmpty(senderUsername))
            {
                // در حال حاضر از نام کاربری از کوئری استرینگ استفاده می‌کنیم
                var senderIdFromConnection = await _userConnectionService.GetUserIdForConnection(Context.ConnectionId);
                var senderUser = await _userService.GetUserById(senderIdFromConnection);
                if (senderUser != null)
                {
                    senderUsername = senderUser.Username;
                }
            }

            var recipientUser = await _userService.GetUserByUsername(recipientUsername);
            var senderUserForId = await _userService.GetUserByUsername(senderUsername);

            if (recipientUser == null || senderUserForId == null)
            {
                await Clients.Caller.SendAsync("ErrorMessage", "کاربر گیرنده یا فرستنده یافت نشد.");
                return;
            }

            // ذخیره پیام در دیتابیس
            var sentMessage = await _messageService.SaveMessage(senderUserForId.Id, recipientUser.Id, message);

            // ارسال پیام به گیرنده اگر آنلاین باشد
            var recipientConnections = await _userConnectionService.GetConnectionsForUser(recipientUser.Id);
            if (recipientConnections.Any())
            {
                foreach (var connectionId in recipientConnections)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", senderUsername, message);
                }
                // پیام بلافاصله به عنوان خوانده شده علامت زده نمیشه تا گیرنده ببینه
            }
            else
            {
                await Clients.Caller.SendAsync("ErrorMessage", $"کاربر '{recipientUsername}' در حال حاضر آنلاین نیست. پیام ذخیره شد و به محض آنلاین شدن دریافت خواهد کرد.");
            }
        }


        private async Task SendConnectedUsers()
        {
            var onlineUsers = await _userConnectionService.GetOnlineUsers();
            await Clients.All.SendAsync("ConnectedUsers", onlineUsers.Select(u => u.Username).ToList());
        }
    }
}

