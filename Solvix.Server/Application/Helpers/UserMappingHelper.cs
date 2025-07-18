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
                UserName = user.UserName ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Token = token,
                FcmToken = user.FcmToken,
                IsOnline = isOnline,
                LastActiveAt = user.LastActiveAt
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

        public static GroupMemberDto MapToGroupMemberDto(GroupMember member)
        {
            return new GroupMemberDto
            {
                Id = member.UserId,
                Username = member.User?.Username ?? "",
                FirstName = member.User?.FirstName,
                LastName = member.User?.LastName,
                ProfilePictureUrl = member.User?.ProfilePictureUrl,
                Role = member.Role.ToString(),
                JoinedAt = member.JoinedAt,
                IsOnline = member.User?.IsOnline ?? false,
                LastActive = member.User?.LastActiveAt
            };
        }

        public static GroupSettingsDto MapToGroupSettingsDto(GroupSettings? settings)
        {
            if (settings == null)
            {
                return new GroupSettingsDto();
            }

            return new GroupSettingsDto
            {
                MaxMembers = settings.MaxMembers,
                OnlyAdminsCanSendMessages = settings.OnlyAdminsCanSendMessages,
                OnlyAdminsCanAddMembers = settings.OnlyAdminsCanAddMembers,
                OnlyAdminsCanEditInfo = settings.OnlyAdminsCanEditInfo,
                OnlyAdminsCanDeleteMessages = settings.OnlyAdminsCanDeleteMessages,
                AllowMemberToLeave = settings.AllowMemberToLeave,
                IsPublic = settings.IsPublic,
                JoinLink = settings.JoinLink
            };
        }
    }
}