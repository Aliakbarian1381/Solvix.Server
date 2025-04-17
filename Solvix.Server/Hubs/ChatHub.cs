using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Solvix.Server.Data;
using Solvix.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Solvix.Server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatHub> _logger;
        private readonly ChatDbContext _context;

        public ChatHub(IUserConnectionService userConnectionService, ILogger<ChatHub> logger, ChatDbContext context)
        {
            _userConnectionService = userConnectionService;
            _logger = logger;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (long.TryParse(userId, out var uid))
            {
                await _userConnectionService.AddConnection(uid, Context.ConnectionId);
                await SendConnectedUsers();

                // اضافه کردن این قسمت
                await SendUnreadMessagesToUser(uid);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var connectionId = Context.ConnectionId;
            var userId = await _userConnectionService.GetUserIdForConnection(connectionId);
            if (userId != null)
            {
                await _userConnectionService.RemoveConnection(connectionId);
                await SendConnectedUsers();
            }
            await base.OnDisconnectedAsync(ex);
        }

        public async Task SendToChat(Guid chatId, string message)
        {
            var userId = long.Parse(Context.UserIdentifier!);

            // 1. ذخیره پیام در دیتابیس
            var newMessage = new Message
            {
                ChatId = chatId,
                SenderId = userId,
                Content = message,
                SentAt = DateTime.UtcNow
            };
            _context.Messages.Add(newMessage);
            await _context.SaveChangesAsync();

            // 2. ارسال پیام به تمام اعضای آنلاین چت
            var chatParticipants = await _context.ChatParticipants
                .Where(cp => cp.ChatId == chatId)
                .ToListAsync();

            foreach (var participant in chatParticipants)
            {
                var connectionIds = await _userConnectionService.GetConnectionsForUser(participant.UserId);
                foreach (var connectionId in connectionIds)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync(
                            "ReceiveMessage",
                            Context.User.Identity?.Name,
                            message,
                            chatId
                        );
                    }
                }
                // اگه کاربر آفلاین بود، پیام فقط تو دیتابیس ذخیره شده و بعدا بهش نشون داده میشه
            }
        }

        private async Task SendConnectedUsers()
        {
            var onlineUsers = await _userConnectionService.GetOnlineUsers();
            await Clients.All.SendAsync("ConnectedUsers", onlineUsers.Select(u => u.UserName).ToList());
        }

        // این متد برای ارسال پیام های خوانده نشده به کاربر بعد از اتصال
        private async Task SendUnreadMessagesToUser(long userId)
        {
            var unreadMessages = await _context.Messages
                .Where(m => m.ChatId == m.ChatId && m.IsRead == false)
                .ToListAsync();

            var connectionIds = await _userConnectionService.GetConnectionsForUser(userId);
            foreach (var connectionId in connectionIds)
            {
                if (!string.IsNullOrEmpty(connectionId))
                {
                    foreach (var message in unreadMessages)
                    {
                        await Clients.Client(connectionId).SendAsync(
                            "ReceiveMessage",
                            message.Sender.FirstName + " " + message.Sender.LastName, // یا نام مناسب
                            message.Content,
                            message.ChatId
                        );
                    }
                }
            }

            // بعد از ارسال پیام ها، اونها رو به عنوان 'IsRead = true' علامت بزن
            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }
    }
}