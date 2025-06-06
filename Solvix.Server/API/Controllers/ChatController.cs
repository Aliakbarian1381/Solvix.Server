﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class ChatController : BaseController
    {
        private readonly IChatService _chatService;
        private readonly IUserService _userService;

        public ChatController(
            IChatService chatService,
            IUserService userService,
            ILogger<ChatController> logger) : base(logger)
        {
            _chatService = chatService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserChats()
        {
            try
            {
                long userId = GetUserId();
                var chats = await _chatService.GetUserChatsAsync(userId);
                return Ok(chats);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get user chats");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chats for user");
                return ServerError("خطا در دریافت چت‌ها");
            }
        }

        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChat(Guid chatId)
        {
            try
            {
                long userId = GetUserId();
                var chat = await _chatService.GetChatByIdAsync(chatId, userId);

                if (chat == null)
                {
                    return NotFound(new { message = "چت یافت نشد یا شما دسترسی به آن ندارید" });
                }

                return Ok(chat);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get chat {ChatId}", chatId);
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat {ChatId}", chatId);
                return ServerError("خطا در دریافت اطلاعات چت");
            }
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartChatWithUser([FromBody] long recipientUserId)
        {
            try
            {
                long userId = GetUserId();

                if (userId == recipientUserId)
                {
                    return BadRequest("امکان شروع چت با خودتان وجود ندارد.");
                }

                // بررسی وجود کاربر مقصد
                var recipient = await _userService.GetUserByIdAsync(recipientUserId);
                if (recipient == null)
                {
                    return NotFound("کاربر مورد نظر یافت نشد.");
                }

                var result = await _chatService.StartChatWithUserAsync(userId, recipientUserId);
                return Ok(new { chatId = result.chatId, alreadyExists = result.alreadyExists });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to start chat");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat with user {RecipientId}", recipientUserId);
                return ServerError("خطا در ایجاد چت");
            }
        }

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(Guid chatId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                long userId = GetUserId();
                var messages = await _chatService.GetChatMessagesAsync(chatId, userId, skip, take);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get messages for chat {ChatId}", chatId);
                return Forbidden("شما عضو این چت نیستید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for chat {ChatId}", chatId);
                return ServerError("خطا در دریافت پیام‌ها");
            }
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            try
            {
                long userId = GetUserId();

                if (string.IsNullOrWhiteSpace(dto.Content))
                {
                    return BadRequest("متن پیام نمی‌تواند خالی باشد");
                }

                // بررسی عضویت کاربر در چت
                if (!await _chatService.IsUserParticipantAsync(dto.ChatId, userId))
                {
                    return Forbidden("شما عضو این چت نیستید");
                }

                var message = await _chatService.SaveMessageAsync(dto.ChatId, userId, dto.Content);
                await _chatService.BroadcastMessageAsync(message);

                var messageDto = MappingHelper.MapToMessageDto(message);
                return Ok(messageDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to send message to chat {ChatId}", dto.ChatId);
                return Forbidden("شما عضو این چت نیستید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ChatId}", dto.ChatId);
                return ServerError("خطا در ارسال پیام");
            }
        }

        [HttpPut("messages/{messageId}")]
        public async Task<IActionResult> EditMessage(int messageId, [FromBody] string newContent)
        {
            if (string.IsNullOrWhiteSpace(newContent))
            {
                return BadRequest("محتوای جدید نمی‌تواند خالی باشد.");
            }
            try
            {
                long userId = GetUserId();
                var editedMessage = await _chatService.EditMessageAsync(messageId, newContent, userId);

                if (editedMessage == null)
                {
                    return Forbidden("شما اجازه ویرایش این پیام را ندارید یا پیام وجود ندارد.");
                }

                // اطلاع‌رسانی به سایر کاربران از طریق SignalR
                await _chatService.BroadcastMessageUpdateAsync(editedMessage);

                return Ok(MappingHelper.MapToMessageDto(editedMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId}", messageId);
                return ServerError("خطا در ویرایش پیام.");
            }
        }

        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            try
            {
                long userId = GetUserId();
                var deletedMessage = await _chatService.DeleteMessageAsync(messageId, userId);

                if (deletedMessage == null)
                {
                    return Forbidden("شما اجازه حذف این پیام را ندارید یا پیام وجود ندارد.");
                }

                // اطلاع‌رسانی به سایر کاربران از طریق SignalR
                await _chatService.BroadcastMessageUpdateAsync(deletedMessage);

                return Ok(new { message = "پیام با موفقیت حذف شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                return ServerError("خطا در حذف پیام.");
            }
        }

        [HttpPost("{chatId}/mark-read")]
        public async Task<IActionResult> MarkMessagesAsRead(Guid chatId, [FromBody] List<int> messageIds)
        {
            try
            {
                long userId = GetUserId();

                // بررسی عضویت کاربر در چت
                if (!await _chatService.IsUserParticipantAsync(chatId, userId))
                {
                    return Forbidden("شما عضو این چت نیستید");
                }

                await _chatService.MarkMultipleMessagesAsReadAsync(messageIds, userId);
                return Ok();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to mark messages as read in chat {ChatId}", chatId);
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in chat {ChatId}", chatId);
                return ServerError("خطا در بروزرسانی وضعیت پیام‌ها");
            }
        }
    }
}