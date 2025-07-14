using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Solvix.Server.Application.Helpers
{
    public static class MappingHelper
    {
        public static UserDto MapToUserDto(AppUser user, bool isOnline, string? token = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "AppUser cannot be null for mapping.");
            }

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? "",  // تغییر از Username به UserName
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                FcmToken = token,  // تغییر از Token به FcmToken
                IsOnline = isOnline,
                LastActiveAt = user.LastActiveAt  // تغییر از LastActive به LastActiveAt
            };
        }

        public static MessageDto MapToMessageDto(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message), "Message cannot be null for mapping.");
            }

            var senderName = message.Sender != null
                             ? $"{message.Sender.FirstName} {message.Sender.LastName}".Trim()
                             : "کاربر نامشخص";

            return new MessageDto
            {
                Id = message.Id,
                Content = message.Content,
                SentAt = message.SentAt,
                SenderId = message.SenderId,
                SenderName = senderName,
                ChatId = message.ChatId,
                IsRead = message.IsRead,
                ReadAt = message.ReadAt,
                IsEdited = message.IsEdited,
                EditedAt = message.EditedAt,
                IsDeleted = message.IsDeleted
            };
        }

        public static ChatDto MapToChatDto(Chat chat, long currentUserId, Dictionary<long, bool> onlineStatuses)
        {
            if (chat == null) { throw new ArgumentNullException(nameof(chat)); }

            string? title = chat.Title;

            if (!chat.IsGroup && chat.Participants != null)
            {
                var otherParticipant = chat.Participants.FirstOrDefault(p => p.UserId != currentUserId);
                if (otherParticipant?.User != null)
                {
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"{otherParticipant.User.FirstName} {otherParticipant.User.LastName}".Trim();
                    }
                }
            }

            var lastMessage = chat.Messages?.Where(m => !m.IsDeleted).OrderByDescending(m => m.SentAt).FirstOrDefault();
            var unreadCount = chat.Messages?.Count(m => m.SenderId != currentUserId && !m.IsRead) ?? 0;

            return new ChatDto
            {
                Id = chat.Id,
                IsGroup = chat.IsGroup,
                Title = title ?? (chat.IsGroup ? "گروه" : "چت"),
                CreatedAt = chat.CreatedAt,
                LastMessage = lastMessage?.Content,
                LastMessageTime = lastMessage?.SentAt,
                UnreadCount = unreadCount,
                Participants = chat.Participants?.Select(p => MapToUserDto(
                                                                p.User,
                                                                onlineStatuses.GetValueOrDefault(p.UserId, false)
                                                            )).ToList() ?? new List<UserDto>()
            };
        }
    }
}