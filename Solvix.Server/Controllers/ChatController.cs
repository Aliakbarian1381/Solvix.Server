using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solvix.Server.Data;
using Solvix.Server.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Solvix.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ChatDbContext _context;

        public ChatController(ChatDbContext context)
        {
            _context = context;
        }

        private long GetUserId() =>
            long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetUserChats()
        {
            var userId = GetUserId();

            var chats = await _context.ChatParticipants
    .Where(cp => cp.UserId == userId)
    .Select(cp => new
    {
        cp.Chat.Id,
        cp.Chat.IsGroup,
        cp.Chat.Title,
        LastMessage = cp.Chat.Messages
            .OrderByDescending(m => m.SentAt)
            .Select(m => m.Content)
            .FirstOrDefault() ?? "",
        LastMessageTime = cp.Chat.Messages
            .OrderByDescending(m => m.SentAt)
            .Select(m => m.SentAt)
            .FirstOrDefault(),
        UnreadCount = cp.Chat.Messages.Count(m => m.SenderId != userId && !m.IsRead)
    })
    .ToListAsync();

            return Ok(chats);
        }

        // ✅ شروع چت (اگه نباشه می‌سازه)
        [HttpPost("start")]
        public async Task<IActionResult> StartChatWithUser([FromBody] long recipientUserId)
        {
            var userId = GetUserId();

            // چک کن چت دو نفره قبلاً وجود داره یا نه
            var existingChat = await _context.Chats
                .Where(c => !c.IsGroup &&
                    c.Participants.Any(p => p.UserId == userId) &&
                    c.Participants.Any(p => p.UserId == recipientUserId))
                .FirstOrDefaultAsync();

            if (existingChat != null)
            {
                return Ok(new { existingChat.Id, alreadyExists = true });
            }

            // ساخت چت جدید
            var chat = new Chat
            {
                IsGroup = false
            };
            _context.Chats.Add(chat);
            _context.ChatParticipants.AddRange(
                new ChatParticipant { Chat = chat, UserId = userId },
                new ChatParticipant { Chat = chat, UserId = recipientUserId }
            );
            await _context.SaveChangesAsync();

            return Ok(new { chat.Id, alreadyExists = false });
        }

        // ✅ گرفتن پیام‌های یک چت خاص
        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(Guid chatId)
        {
            var userId = GetUserId();

            // امنیت: آیا این کاربر عضو این چته؟
            var isParticipant = await _context.ChatParticipants
                .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

            if (!isParticipant)
                return Forbid();

            // ✅ علامت زدن پیام‌ها به عنوان خوانده شده توسط کاربر فعلی
            var unreadMessages = await _context.Messages
                .Where(m => m.ChatId == chatId && m.SenderId != userId && m.IsRead == false)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }
            await _context.SaveChangesAsync();

            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.SentAt,
                    m.SenderId,
                    SenderName = m.Sender.FirstName + " " + m.Sender.LastName
                })
                .ToListAsync();

            return Ok(messages);
        }


        [HttpPost("/api/messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // اطمینان از عضویت در چت
            var isParticipant = await _context.ChatParticipants
                .AnyAsync(cp => cp.ChatId == dto.ChatId && cp.UserId == userId);

            if (!isParticipant)
                return Forbid();

            var message = new Message
            {
                ChatId = dto.ChatId,
                SenderId = userId,
                Content = dto.Content,
                SentAt = DateTime.UtcNow,
                IsRead = false // پیام جدید خوانده نشده است
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return Ok();
        }

        public class SendMessageDto
        {
            public Guid ChatId { get; set; }
            public string Content { get; set; } = "";
        }

    }
}