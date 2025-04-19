using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Solvix.Server.Data;
using Solvix.Server.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Solvix.Server.Dtos;

namespace Solvix.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : BaseController
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ChatDbContext context, ILogger<ChatController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> GetUserChats()
        {
            long userId;
            try
            {
                userId = GetUserId();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get user chats: {Message}", ex.Message);
                return Unauthorized("User ID could not be determined.");
            }


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

        [HttpPost("start")]
        public async Task<IActionResult> StartChatWithUser([FromBody] long recipientUserId)
        {
            long userId;
            try
            {
                userId = GetUserId();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to start chat: {Message}", ex.Message);
                return Unauthorized("User ID could not be determined.");
            }

            if (userId == recipientUserId)
            {
                return BadRequest(new { message = "Cannot start a chat with yourself." });
            }

            var existingChat = await _context.ChatParticipants
               .Where(cp => (cp.UserId == userId || cp.UserId == recipientUserId))
               .GroupBy(cp => cp.ChatId)
               .Where(g => g.Count() == 2 && g.All(cp => cp.Chat.IsGroup == false))
               .Select(g => g.Key)
               .FirstOrDefaultAsync();


            if (existingChat != Guid.Empty)
            {
                return Ok(new { chatId = existingChat, alreadyExists = true });
            }


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

            return Ok(new { chatId = chat.Id, alreadyExists = false });
        }

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(Guid chatId)
        {
            long userId;
            try
            {
                userId = GetUserId();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get chat messages: {Message}", ex.Message);
                return Unauthorized("User ID could not be determined.");
            }

            var isParticipant = await IsUserParticipant(_context, chatId, userId);

            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to access messages in chat {ChatId} without being a participant.", userId, chatId);
                return Forbid();
            }


            var unreadMessages = await _context.Messages
                .Where(m => m.ChatId == chatId && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }
            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }


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


        [HttpPost("/api/send-messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            long userId;
            try
            {
                userId = GetUserId();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to send message: {Message}", ex.Message);
                return Unauthorized("User ID could not be determined.");
            }

            var isParticipant = await IsUserParticipant(_context, dto.ChatId, userId);


            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to send message to chat {ChatId} without being a participant.", userId, dto.ChatId);
                return Forbid();
            }

            // این منطق ساخت و ذخیره پیام قرار است به ChatService منتقل شود.
            // در حال حاضر، این متد فقط پیام را ذخیره می‌کند. ارسال پیام به کلاینت‌ها توسط ChatService و ChatHub انجام خواهد شد
            // نقطه پایانی API برای ارسال پیام ممکن است توسط سرویس‌های دیگر یا برای مواردی که SignalR در دسترس نیست استفاده شود.
            // اگر از ChatService استفاده میکنید، این بخش باید پیام را به سرویس بفرستد نه اینکه خودش ذخیره کند.

            // منطق ذخیره پیام (به ChatService منتقل خواهد شد)
            var message = new Message
            {
                ChatId = dto.ChatId,
                SenderId = userId,
                Content = dto.Content,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);

            try
            {
                await _context.SaveChangesAsync();
                // اگر این منطق به سرویس منتقل شود، سرویس پیام ذخیره شده را برمیگرداند و اینجا میتوانید از آن استفاده کنید.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving message in ChatController for Chat {ChatId} by User {UserId}.", dto.ChatId, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error saving message.");
            }


            return Ok();
        }

    }
}