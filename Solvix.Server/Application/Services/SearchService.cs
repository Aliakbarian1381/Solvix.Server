using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solvix.Server.Application.Services
{
    public class SearchService : ISearchService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserConnectionService _userConnectionService;

        public SearchService(IUnitOfWork unitOfWork, IUserConnectionService userConnectionService)
        {
            _unitOfWork = unitOfWork;
            _userConnectionService = userConnectionService;
        }

        public async Task<List<SearchResultDto>> SearchAsync(string query, long currentUserId)
        {
            var results = new List<SearchResultDto>();

            // 1. Search in existing chats
            var chats = await _unitOfWork.ChatRepository.SearchUserChatsAsync(currentUserId, query);
            var onlineStatuses = new Dictionary<long, bool>(); // To optimize online status checks

            foreach (var chat in chats)
            {
                // Preload online statuses for participants to avoid multiple DB calls
                foreach (var participant in chat.Participants)
                {
                    if (!onlineStatuses.ContainsKey(participant.UserId))
                    {
                        onlineStatuses[participant.UserId] = await _userConnectionService.IsUserOnlineAsync(participant.UserId);
                    }
                }

                var chatDto = MappingHelper.MapToChatDto(chat, currentUserId, onlineStatuses);
                results.Add(new SearchResultDto
                {
                    Id = chat.Id.ToString(),
                    Title = chatDto.Title ?? "چت",
                    Subtitle = chat.IsGroup ? $"{chat.Participants.Count} عضو" : chatDto.LastMessage ?? "شروع گفتگو...",
                    Type = "chat",
                    Entity = chatDto
                });
            }

            // 2. Search for users to start a new chat
            var users = await _unitOfWork.UserRepository.SearchUsersAsync(query, 10);
            foreach (var user in users)
            {
                if (user.Id == currentUserId) continue; // Don't show self

                // Check if a chat already exists with this user
                var privateChatExists = chats.Any(c => !c.IsGroup && c.Participants.Any(p => p.UserId == user.Id));
                if (privateChatExists) continue; // If chat exists, it's already in the results list

                if (!onlineStatuses.ContainsKey(user.Id))
                {
                    onlineStatuses[user.Id] = await _userConnectionService.IsUserOnlineAsync(user.Id);
                }
                var userDto = MappingHelper.MapToUserDto(user, onlineStatuses[user.Id]);
                results.Add(new SearchResultDto
                {
                    Id = user.Id.ToString(),
                    Title = userDto.FullName ?? user.UserName ?? "کاربر",
                    Subtitle = user.PhoneNumber,
                    Type = "user",
                    Entity = userDto
                });
            }

            return results;
        }
    }
}