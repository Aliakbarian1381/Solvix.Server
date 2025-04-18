using Microsoft.AspNetCore.SignalR;
using Solvix.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Solvix.Server.Data;
using Solvix.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Solvix.Server.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IUserConnectionService _userConnectionService;
        private readonly ILogger<ChatHub> _logger;
        private readonly ChatDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ChatHub(
            IUserConnectionService userConnectionService,
            ILogger<ChatHub> logger,
            ChatDbContext context,
            UserManager<AppUser> userManager) 
        {
            _userConnectionService = userConnectionService;
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdString = Context.UserIdentifier;
            if (long.TryParse(userIdString, out var uid))
            {
                await _userConnectionService.AddConnection(uid, Context.ConnectionId);
                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", uid, Context.ConnectionId);
                // await SendConnectedUsers(); // ارسال لیست کاربران آنلاین شاید همیشه لازم نباشه یا باید بهینه‌تر بشه

                // ارسال پیام‌های خوانده نشده (با کوئری اصلاح شده)
                // await SendUnreadMessagesToUser(uid); // این بخش رو طبق توضیحات قبلی فعلا کامنت می‌کنیم یا حذف می‌کنیم
            }
            else
            {
                _logger.LogWarning("Could not parse UserIdentifier '{UserIdentifier}' to long.", userIdString);
            }
            await base.OnConnectedAsync();
        }

        // --- متد OnDisconnectedAsync ---
        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var connectionId = Context.ConnectionId;
            var userId = await _userConnectionService.GetUserIdForConnection(connectionId); // userId رو بگیر که بدونی کی قطع شده
            await _userConnectionService.RemoveConnection(connectionId);
            if (userId.HasValue)
            {
                _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}. Error: {Error}", userId.Value, connectionId, ex?.Message);
                // await SendConnectedUsers(); // آپدیت لیست کاربران آنلاین برای بقیه
            }
            else
            {
                _logger.LogWarning("Connection {ConnectionId} disconnected without a mapped user. Error: {Error}", connectionId, ex?.Message);
            }

            await base.OnDisconnectedAsync(ex);
        }

        // --- متد ارسال پیام به چت (اصلاح شده برای ارسال پارامترهای کامل) ---
        public async Task SendToChat(Guid chatId, string messageContent) // فقط محتوای پیام دریافت بشه کافیه
        {
            var userIdString = Context.UserIdentifier;
            if (!long.TryParse(userIdString, out var senderUserId))
            {
                _logger.LogError("Cannot send message. Invalid UserIdentifier: {UserIdentifier}", userIdString);
                // می‌تونی یک خطا به کلاینت بفرستی
                await Clients.Caller.SendAsync("ReceiveError", "Authentication error. Cannot send message.");
                return;
            }

            // 1. اطمینان از عضویت فرستنده در چت (امنیت)
            var isParticipant = await _context.ChatParticipants
                .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == senderUserId);

            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to send message to Chat {ChatId} without being a participant.", senderUserId, chatId);
                await Clients.Caller.SendAsync("ReceiveError", "You are not a member of this chat.");
                return;
            }

            // 2. پیدا کردن اطلاعات فرستنده (برای نام)
            // AppUser sender = await _userManager.FindByIdAsync(senderUserId.ToString());
            // یا اگر Sender رو در مدل Message لود می‌کنیم:
            var sender = await _context.Users.FindAsync(senderUserId); // سریع‌تره اگر فقط User رو بخوایم

            if (sender == null)
            {
                _logger.LogError("Cannot send message. Sender user with ID {UserId} not found.", senderUserId);
                await Clients.Caller.SendAsync("ReceiveError", "User profile not found. Cannot send message.");
                return;
            }

            // 3. ساخت و ذخیره پیام در دیتابیس
            var newMessage = new Message
            {
                ChatId = chatId,
                SenderId = senderUserId,
                Content = messageContent, // از ورودی متد
                SentAt = DateTime.UtcNow,
                IsRead = false // پیام جدید خوانده نشده است
                // Sender property will be null here unless loaded, but we have sender object above
            };
            _context.Messages.Add(newMessage);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Message {MessageId} saved for Chat {ChatId} by User {UserId}", newMessage.Id, chatId, senderUserId);

                // 4. ارسال پیام به تمام اعضای آنلاین چت (به جز خود فرستنده، مگر اینکه بخواهیم خودش هم دریافت کند)
                var chatParticipants = await _context.ChatParticipants
                    .Where(cp => cp.ChatId == chatId /*&& cp.UserId != senderUserId*/) // اگه نمی‌خوای به خودش بفرستی، این شرط رو اضافه کن
                    .Select(cp => cp.UserId)
                    .ToListAsync();

                var senderFullName = $"{sender.FirstName} {sender.LastName}".Trim(); // نام کامل فرستنده

                foreach (var participantUserId in chatParticipants)
                {
                    var connectionIds = await _userConnectionService.GetConnectionsForUser(participantUserId);
                    foreach (var connectionId in connectionIds)
                    {
                        if (!string.IsNullOrEmpty(connectionId))
                        {
                            try
                            {
                                await Clients.Client(connectionId).SendAsync(
                                    "ReceiveMessage", // نام رویداد در کلاینت
                                    newMessage.Id,        // ID پیام (int)
                                    newMessage.SenderId,    // ID فرستنده (long)
                                    senderFullName,         // نام فرستنده (string)
                                    newMessage.Content,     // محتوای پیام (string)
                                    newMessage.ChatId,      // ID چت (Guid)
                                    newMessage.SentAt       // زمان ارسال UTC (DateTime)
                                );
                                _logger.LogInformation("Sent message {MessageId} to User {UserId} on connection {ConnectionId}", newMessage.Id, participantUserId, connectionId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending message {MessageId} to User {UserId} on connection {ConnectionId}", newMessage.Id, participantUserId, connectionId);
                            }
                        }
                    }
                    // نکته: اگر کاربر آفلاین بود، پیام فقط در دیتابیس ذخیره شده و بعداً هنگام GetMessages دریافت می‌شود.
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save message to database for Chat {ChatId} by User {UserId}.", chatId, senderUserId);
                await Clients.Caller.SendAsync("ReceiveError", "Failed to save message. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in SendToChat for Chat {ChatId} by User {UserId}.", chatId, senderUserId);
                await Clients.Caller.SendAsync("ReceiveError", "An unexpected error occurred.");
            }
        }

        // --- (اختیاری) متد برای آپدیت وضعیت خوانده شدن ---
        public async Task MarkMessageAsRead(int messageId)
        {
            var userIdString = Context.UserIdentifier;
            if (!long.TryParse(userIdString, out var readerUserId)) return;

            var message = await _context.Messages
                                  .Include(m => m.Chat) // برای دسترسی به ChatId و سایر اعضا
                                  .ThenInclude(c => c.Participants)
                                  .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.SenderId == readerUserId || message.IsRead)
            {
                // پیام وجود نداره، مال خود کاربره، یا قبلا خوانده شده
                return;
            }

            // اطمینان از اینکه خواننده عضو چت است
            if (!message.Chat.Participants.Any(p => p.UserId == readerUserId))
            {
                _logger.LogWarning("User {UserId} tried to mark message {MessageId} as read in chat {ChatId} without being a participant.", readerUserId, messageId, message.ChatId);
                return;
            }


            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow; // زمان خوانده شدن
            await _context.SaveChangesAsync();

            // حالا به فرستنده اصلی پیام (اگر آنلاینه) خبر بده که پیامش خونده شد
            var senderConnectionIds = await _userConnectionService.GetConnectionsForUser(message.SenderId);
            foreach (var connectionId in senderConnectionIds)
            {
                if (!string.IsNullOrEmpty(connectionId))
                {
                    await Clients.Client(connectionId).SendAsync(
                        "MessageRead", // رویداد جدید در کلاینت
                        message.ChatId,
                        message.Id
                    );
                    _logger.LogInformation("Notified sender {SenderId} on connection {ConnectionId} that message {MessageId} was read.", message.SenderId, connectionId, messageId);
                }
            }
        }


        // --- متد SendConnectedUsers (اگر لازم بود) ---
        // private async Task SendConnectedUsers()
        // {
        //     var onlineUsers = await _userConnectionService.GetOnlineUsers();
        //     // به جای ارسال فقط نام کاربری، شاید بهتر باشه UserDto رو بفرستیم
        //     var onlineUsersDto = onlineUsers.Select(u => new { u.Id, u.FirstName, u.LastName }).ToList();
        //     await Clients.All.SendAsync("ConnectedUsers", onlineUsersDto);
        // }

        // --- متد SendUnreadMessagesToUser (با کوئری اصلاح شده - فعلا غیرفعال) ---
        /*
        private async Task SendUnreadMessagesToUser(long userId)
        {
            _logger.LogInformation("Attempting to send unread messages to User {UserId}", userId);
            try
            {
                var chatIds = await _context.ChatParticipants
                                    .Where(cp => cp.UserId == userId)
                                    .Select(cp => cp.ChatId)
                                    .ToListAsync();

                if (!chatIds.Any())
                {
                    _logger.LogInformation("User {UserId} has no chats.", userId);
                    return;
                }

                var unreadMessages = await _context.Messages
                    .Include(m => m.Sender) // Include Sender for SenderName
                    .Where(m => chatIds.Contains(m.ChatId) && m.SenderId != userId && !m.IsRead)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                 _logger.LogInformation("Found {Count} unread messages for User {UserId}", unreadMessages.Count, userId);

                var connectionIds = await _userConnectionService.GetConnectionsForUser(userId);
                foreach (var connectionId in connectionIds)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        foreach (var message in unreadMessages)
                        {
                            var senderFullName = $"{message.Sender.FirstName} {message.Sender.LastName}".Trim();
                            await Clients.Client(connectionId).SendAsync(
                                "ReceiveMessage",
                                message.Id,
                                message.SenderId,
                                senderFullName,
                                message.Content,
                                message.ChatId,
                                message.SentAt
                            );
                             _logger.LogInformation("Sent unread message {MessageId} to User {UserId} on connection {ConnectionId}", message.Id, userId, connectionId);

                            // !!! مهم: اینجا پیام رو خوانده شده نکنیم !!!
                            // message.IsRead = true; // <<< این کار اشتباه است
                        }
                    }
                }
                // await _context.SaveChangesAsync(); // <<< نیازی به ذخیره تغییرات نیست چون IsRead رو تغییر ندادیم
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending unread messages to User {UserId}", userId);
            }
        }
        */
    }
}