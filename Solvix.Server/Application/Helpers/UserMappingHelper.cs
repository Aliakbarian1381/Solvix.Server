using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Application.Helpers
{
    public static class MappingHelper
    {
        public static UserDto MapToUserDto(AppUser user, string? token = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "AppUser cannot be null for mapping.");
            }

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Token = token ?? "",
                IsOnline = true,
                LastActive = user.LastActiveAt
            };
        }

        public static MessageDto MapToMessageDto(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message), "Message cannot be null for mapping.");
            }

            return new MessageDto
            {
                Id = message.Id,
                Content = message.Content,
                SentAt = message.SentAt,
                SenderId = message.SenderId,
                SenderName = message.Sender != null ? $"{message.Sender.FirstName} {message.Sender.LastName}".Trim() : "",
                ChatId = message.ChatId,
                IsRead = message.IsRead,
                ReadAt = message.ReadAt,
                IsEdited = false, // فعلا بدون پشتیبانی از ویرایش
                EditedAt = null
            };
        }

        public static ChatDto MapToChatDto(Chat chat, long currentUserId)
        {
            if (chat == null)
            {
                throw new ArgumentNullException(nameof(chat), "Chat cannot be null for mapping.");
            }

            string? title = chat.Title;
            if (!chat.IsGroup && string.IsNullOrWhiteSpace(title))
            {
                var otherParticipant = chat.Participants?.FirstOrDefault(p => p.UserId != currentUserId)?.User;
                if (otherParticipant != null)
                {
                    title = $"{otherParticipant.FirstName} {otherParticipant.LastName}".Trim();
                }
            }

            var lastMessage = chat.Messages?.OrderByDescending(m => m.SentAt).FirstOrDefault();
            var unreadCount = chat.Messages?.Count(m => m.SenderId != currentUserId && !m.IsRead) ?? 0;

            return new ChatDto
            {
                Id = chat.Id,
                IsGroup = chat.IsGroup,
                Title = title,
                CreatedAt = chat.CreatedAt,
                LastMessage = lastMessage?.Content,
                LastMessageTime = lastMessage?.SentAt,
                UnreadCount = unreadCount,
                Participants = chat.Participants?.Select(p => MapToUserDto(p.User)).ToList() ?? new List<UserDto>()
            };
        }
    }
}