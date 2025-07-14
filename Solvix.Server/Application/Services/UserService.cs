using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;



namespace Solvix.Server.Application.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserService> _logger;
        private readonly IUserConnectionService _connectionService;

        public UserService(
            UserManager<AppUser> userManager,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            ILogger<UserService> logger,
            IUserConnectionService connectionService)
        {
            _userManager = userManager;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _connectionService = connectionService;
        }

        public async Task<AppUser?> GetUserByUsernameAsync(string username)
        {
            return await _userRepository.GetByUsernameAsync(username);
        }

        public async Task<AppUser?> GetUserByIdAsync(long userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<IdentityResult> CreateUserAsync(AppUser user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }

        public async Task<bool> CheckPhoneExistsAsync(string phoneNumber)
        {
            return await _userRepository.PhoneNumberExistsAsync(phoneNumber);
        }

        public async Task<List<UserDto>> SearchUsersAsync(string searchTerm, long currentUserId, int limit = 20)
        {
            try
            {
                var users = await _userRepository.SearchUsersAsync(searchTerm, limit);
                users = users.Where(u => u.Id != currentUserId).ToList();

                var userDtos = new List<UserDto>();
                foreach (var user in users)
                {
                    bool isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                    userDtos.Add(MappingHelper.MapToUserDto(user, isOnline));
                }
                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term: {SearchTerm}", searchTerm);
                return new List<UserDto>();
            }
        }

        public async Task<IEnumerable<UserDto>> FindUsersByPhoneNumbersAsync(IEnumerable<string> phoneNumbers, long currentUserId)
        {
            try
            {
                var usersFromRepo = await _unitOfWork.UserRepository.FindUsersByPhoneNumbersAsync(phoneNumbers);
                var filteredUsers = usersFromRepo.Where(u => u.Id != currentUserId).ToList();

                // +++ اصلاح شده: استفاده صحیح از async/await +++
                var existingContactRelations = await _unitOfWork.UserContactRepository
                    .ListAsync(uc => uc.OwnerUserId == currentUserId &&
                                   filteredUsers.Select(u => u.Id).Contains(uc.ContactUserId));
                var existingContactIds = existingContactRelations.Select(uc => uc.ContactUserId).ToHashSet();

                var newContacts = filteredUsers.Where(u => !existingContactIds.Contains(u.Id)).ToList();

                if (newContacts.Any())
                {
                    var contactsToAdd = newContacts.Select(user => new UserContact
                    {
                        OwnerUserId = currentUserId,
                        ContactUserId = user.Id,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    await _unitOfWork.UserContactRepository.AddRangeAsync(contactsToAdd);
                    await _unitOfWork.CompleteAsync();
                }

                var userDtos = new List<UserDto>();
                foreach (var user in filteredUsers)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                    userDtos.Add(MappingHelper.MapToUserDto(user, isOnline));
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding users by phone numbers for user {UserId}", currentUserId);
                return new List<UserDto>();
            }
        }

        public async Task<IEnumerable<UserDto>> GetSavedContactsAsync(long ownerUserId)
        {
            try
            {
                // +++ اصلاح شده: استفاده صحیح از async/await و OrderBy +++
                var savedContactRelations = await _unitOfWork.UserContactRepository
                    .ListAsync(uc => uc.OwnerUserId == ownerUserId && !uc.IsBlocked,
                               includeProperties: "ContactUser");

                var orderedRelations = savedContactRelations.OrderBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName).ToList();

                var userDtos = new List<UserDto>();
                foreach (var relation in orderedRelations)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(relation.ContactUser.Id);
                    var dto = MappingHelper.MapToUserDto(relation.ContactUser, isOnline);
                    // می‌توانید اطلاعات دیگر از relation مثل IsFavorite را هم به dto اضافه کنید
                    userDtos.Add(dto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts for user {UserId}", ownerUserId);
                return new List<UserDto>();
            }
        }

        public async Task<IEnumerable<UserDto>> GetSavedContactsWithChatInfoAsync(long ownerUserId)
        {
            try
            {
                // +++ بازنویسی کامل متد برای سادگی و کارایی بهتر +++
                var contacts = (await _unitOfWork.UserContactRepository.ListAsync(
                    uc => uc.OwnerUserId == ownerUserId && !uc.IsBlocked,
                    includeProperties: "ContactUser"
                )).ToList();

                var contactUserIds = contacts.Select(c => c.ContactUserId).ToList();

                var chats = (await _unitOfWork.ChatRepository.ListAsync(
                    c => !c.IsGroup && c.Participants.Any(p => p.UserId == ownerUserId) &&
                         c.Participants.Any(p => contactUserIds.Contains(p.UserId)),
                    includeProperties: "Participants"
                )).ToDictionary(ch => ch.Participants.First(p => p.UserId != ownerUserId).UserId);

                var userDtos = new List<UserDto>();
                foreach (var contact in contacts)
                {
                    var user = contact.ContactUser;
                    var isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                    var userDto = MappingHelper.MapToUserDto(user, isOnline);

                    if (chats.TryGetValue(user.Id, out var chat))
                    {
                        userDto.HasChat = true;
                        userDto.LastMessage = chat.LastMessage;
                        userDto.LastMessageTime = chat.LastMessageTime;
                        userDto.UnreadCount = chat.UnreadCount;
                    }
                    userDtos.Add(userDto);
                }

                return userDtos.OrderBy(u => u.FirstName ?? u.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts with chat info for user {UserId}", ownerUserId);
                return await GetSavedContactsAsync(ownerUserId); // fallback
            }
        }

        public async Task<bool> UpdateFcmTokenAsync(long userId, string fcmToken)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for FCM token update: {UserId}", userId);
                    return false;
                }

                user.FcmToken = fcmToken;
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("FCM token updated for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating FCM token for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdateUserLastActiveAsync(long userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return false;

                user.LastActiveAt = DateTime.UtcNow;
                await _unitOfWork.CompleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last active time for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> MarkContactAsFavoriteAsync(long ownerId, long contactId, bool isFavorite)
        {
            try
            {
                var contact = await _unitOfWork.UserContactRepository.GetUserContactAsync(ownerId, contactId);

                if (contact == null) return false;

                contact.IsFavorite = isFavorite;
                await _unitOfWork.CompleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating favorite status for contact {ContactId} of user {UserId}",
                    contactId, ownerId);
                return false;
            }
        }

        public async Task<bool> BlockContactAsync(long ownerId, long contactId, bool isBlocked)
        {
            try
            {
                var contact = await _unitOfWork.UserContactRepository.GetUserContactAsync(ownerId, contactId);

                if (contact == null) return false;

                contact.IsBlocked = isBlocked;
                await _unitOfWork.CompleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating block status for contact {ContactId} of user {UserId}",
                    contactId, ownerId);
                return false;
            }
        }
    }
}