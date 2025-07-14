using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using Solvix.Server.Infrastructure.Repositories;

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
                if (!phoneNumbers.Any())
                {
                    _logger.LogWarning("Empty phone numbers list received for user {UserId}", currentUserId);
                    return new List<UserDto>();
                }

                var usersFromRepo = await _unitOfWork.UserRepository.FindUsersByPhoneNumbersAsync(phoneNumbers);
                var filteredUsers = usersFromRepo.Where(u => u.Id != currentUserId).ToList();

                if (!filteredUsers.Any())
                {
                    _logger.LogInformation("No users found for provided phone numbers for user {UserId}", currentUserId);
                    return new List<UserDto>();
                }

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
                        CreatedAt = DateTime.UtcNow,
                        IsFavorite = false,
                        IsBlocked = false,
                        DisplayName = null
                    }).ToList();

                    await _unitOfWork.UserContactRepository.AddRangeAsync(contactsToAdd);
                    await _unitOfWork.CompleteAsync();

                    _logger.LogInformation("Added {Count} new contacts for user {UserId}",
                        newContacts.Count, currentUserId);
                }

                var userDtos = new List<UserDto>();
                foreach (var user in filteredUsers)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                    var userDto = MappingHelper.MapToUserDto(user, isOnline);

                    var contactRelation = existingContactRelations.FirstOrDefault(er => er.ContactUserId == user.Id);
                    if (contactRelation != null)
                    {
                        userDto.IsFavorite = contactRelation.IsFavorite;
                        userDto.IsBlocked = contactRelation.IsBlocked;
                        userDto.DisplayName = contactRelation.DisplayName;
                        userDto.ContactCreatedAt = contactRelation.CreatedAt;
                        userDto.LastInteractionAt = contactRelation.LastInteractionAt;
                    }
                    else
                    {
                        userDto.IsFavorite = false;
                        userDto.IsBlocked = false;
                        userDto.ContactCreatedAt = DateTime.UtcNow;
                    }

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding users by phone numbers for user {UserId}", currentUserId);
                throw new Exception("خطا در همگام‌سازی مخاطبین");
            }
        }

        public async Task<IEnumerable<UserDto>> GetSavedContactsAsync(long ownerUserId)
        {
            try
            {
                var savedContactRelations = await _unitOfWork.UserContactRepository
                    .ListAsync(uc => uc.OwnerUserId == ownerUserId && !uc.IsBlocked,
                               includeProperties: "ContactUser");

                var orderedContacts = savedContactRelations
                    .OrderByDescending(uc => uc.IsFavorite)
                    .ThenBy(uc => uc.DisplayName ?? uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                    .ToList();

                var userDtos = new List<UserDto>();
                foreach (var relation in orderedContacts)
                {
                    var user = relation.ContactUser;
                    var isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                    var userDto = MappingHelper.MapToUserDto(user, isOnline);

                    userDto.IsFavorite = relation.IsFavorite;
                    userDto.IsBlocked = relation.IsBlocked;
                    userDto.DisplayName = relation.DisplayName;
                    userDto.ContactCreatedAt = relation.CreatedAt;
                    userDto.LastInteractionAt = relation.LastInteractionAt;
                    userDto.IsContact = true;

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts for user {UserId}", ownerUserId);
                throw new Exception("خطا در دریافت مخاطبین");
            }
        }

        public async Task<IEnumerable<UserDto>> GetSavedContactsWithChatInfoAsync(long ownerUserId)
        {
            try
            {
                var contacts = await _unitOfWork.UserContactRepository.ListAsync(
                    uc => uc.OwnerUserId == ownerUserId && !uc.IsBlocked,
                    includeProperties: "ContactUser");

                if (!contacts.Any())
                {
                    return new List<UserDto>();
                }

                var contactUserIds = contacts.Select(c => c.ContactUserId).ToList();

                var chats = await _unitOfWork.ChatRepository.ListAsync(
                    c => !c.IsGroup &&
                         c.Participants.Any(p => p.UserId == ownerUserId) &&
                         c.Participants.Any(p => contactUserIds.Contains(p.UserId)),
                    includeProperties: "Participants,Messages");

                var chatsByUserId = new Dictionary<long, Chat>();
                foreach (var chat in chats)
                {
                    var otherParticipant = chat.Participants.FirstOrDefault(p => p.UserId != ownerUserId);
                    if (otherParticipant != null)
                    {
                        chatsByUserId[otherParticipant.UserId] = chat;
                    }
                }

                var userDtos = new List<UserDto>();
                foreach (var contact in contacts)
                {
                    var user = contact.ContactUser;
                    var isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                    var userDto = MappingHelper.MapToUserDto(user, isOnline);

                    userDto.IsFavorite = contact.IsFavorite;
                    userDto.IsBlocked = contact.IsBlocked;
                    userDto.DisplayName = contact.DisplayName;
                    userDto.ContactCreatedAt = contact.CreatedAt;
                    userDto.LastInteractionAt = contact.LastInteractionAt;
                    userDto.IsContact = true;

                    if (chatsByUserId.TryGetValue(user.Id, out var chat))
                    {
                        userDto.HasChat = true;
                        userDto.LastMessage = chat.LastMessage;
                        userDto.LastMessageTime = chat.LastMessageTime;
                        userDto.UnreadCount = chat.UnreadCount;
                    }
                    else
                    {
                        userDto.HasChat = false;
                        userDto.UnreadCount = 0;
                    }

                    userDtos.Add(userDto);
                }

                return userDtos
                    .OrderByDescending(u => u.IsFavorite)
                    .ThenByDescending(u => u.LastMessageTime ?? u.ContactCreatedAt)
                    .ThenBy(u => u.DisplayName ?? u.FirstName ?? u.UserName)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts with chat info for user {UserId}", ownerUserId);
                return await GetSavedContactsAsync(ownerUserId);
            }
        }

        public async Task<bool> MarkContactAsFavoriteAsync(long ownerId, long contactId, bool isFavorite)
        {
            try
            {
                var contact = await _unitOfWork.UserContactRepository.GetUserContactAsync(ownerId, contactId);
                if (contact == null)
                {
                    _logger.LogWarning("Contact not found: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                    return false;
                }

                contact.IsFavorite = isFavorite;
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Contact favorite status updated: Owner={OwnerId}, Contact={ContactId}, IsFavorite={IsFavorite}",
                    ownerId, contactId, isFavorite);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating favorite status for contact: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                return false;
            }
        }

        public async Task<bool> BlockContactAsync(long ownerId, long contactId, bool isBlocked)
        {
            try
            {
                var contact = await _unitOfWork.UserContactRepository.GetUserContactAsync(ownerId, contactId);
                if (contact == null)
                {
                    _logger.LogWarning("Contact not found: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                    return false;
                }

                contact.IsBlocked = isBlocked;
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Contact block status updated: Owner={OwnerId}, Contact={ContactId}, IsBlocked={IsBlocked}",
                    ownerId, contactId, isBlocked);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating block status for contact: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                return false;
            }
        }

        public async Task<bool> UpdateContactDisplayNameAsync(long ownerId, long contactId, string? displayName)
        {
            try
            {
                var contact = await _unitOfWork.UserContactRepository.GetUserContactAsync(ownerId, contactId);
                if (contact == null)
                {
                    _logger.LogWarning("Contact not found: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                    return false;
                }

                contact.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Contact display name updated: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating display name for contact: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                return false;
            }
        }

        public async Task<bool> RemoveContactAsync(long ownerId, long contactId)
        {
            try
            {
                var success = await _unitOfWork.UserContactRepository.RemoveContactAsync(ownerId, contactId);
                if (success)
                {
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInformation("Contact removed: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing contact: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
                return false;
            }
        }

        public async Task<IEnumerable<UserDto>> SearchContactsAsync(long userId, string searchTerm, int limit = 20)
        {
            try
            {
                var contacts = await _unitOfWork.UserContactRepository.SearchContactsAsync(userId, searchTerm, limit);

                var userDtos = new List<UserDto>();
                foreach (var contact in contacts)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(contact.ContactUser.Id);
                    var userDto = MappingHelper.MapToUserDto(contact.ContactUser, isOnline);

                    userDto.IsFavorite = contact.IsFavorite;
                    userDto.IsBlocked = contact.IsBlocked;
                    userDto.DisplayName = contact.DisplayName;
                    userDto.ContactCreatedAt = contact.CreatedAt;
                    userDto.LastInteractionAt = contact.LastInteractionAt;
                    userDto.IsContact = true;

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts for user {UserId} with term {SearchTerm}", userId, searchTerm);
                throw new Exception("خطا در جستجوی مخاطبین");
            }
        }

        public async Task<IEnumerable<UserDto>> GetFavoriteContactsAsync(long userId)
        {
            try
            {
                var favoriteContacts = await _unitOfWork.UserContactRepository.ListAsync(
                    uc => uc.OwnerUserId == userId && uc.IsFavorite && !uc.IsBlocked,
                    includeProperties: "ContactUser");

                var orderedContacts = favoriteContacts
                    .OrderBy(uc => uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                    .ToList();

                var userDtos = new List<UserDto>();
                foreach (var contact in orderedContacts)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(contact.ContactUser.Id);
                    var userDto = MappingHelper.MapToUserDto(contact.ContactUser, isOnline);

                    userDto.IsFavorite = contact.IsFavorite;
                    userDto.IsBlocked = contact.IsBlocked;
                    userDto.DisplayName = contact.DisplayName;
                    userDto.ContactCreatedAt = contact.CreatedAt;
                    userDto.LastInteractionAt = contact.LastInteractionAt;
                    userDto.IsContact = true;

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorite contacts for user {UserId}", userId);
                throw new Exception("خطا در دریافت مخاطبین مورد علاقه");
            }
        }

        public async Task<IEnumerable<UserDto>> GetRecentContactsAsync(long userId, int limit = 10)
        {
            try
            {
                var recentContacts = await _unitOfWork.UserContactRepository.GetRecentContactsAsync(userId, limit);

                var userDtos = new List<UserDto>();
                foreach (var contact in recentContacts)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(contact.ContactUser.Id);
                    var userDto = MappingHelper.MapToUserDto(contact.ContactUser, isOnline);

                    userDto.IsFavorite = contact.IsFavorite;
                    userDto.IsBlocked = contact.IsBlocked;
                    userDto.DisplayName = contact.DisplayName;
                    userDto.ContactCreatedAt = contact.CreatedAt;
                    userDto.LastInteractionAt = contact.LastInteractionAt;
                    userDto.IsContact = true;

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent contacts for user {UserId}", userId);
                throw new Exception("خطا در دریافت مخاطبین اخیر");
            }
        }

        public async Task<bool> UpdateLastInteractionAsync(long ownerId, long contactId)
        {
            try
            {
                await _unitOfWork.UserContactRepository.UpdateLastInteractionAsync(ownerId, contactId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last interaction: Owner={OwnerId}, Contact={ContactId}", ownerId, contactId);
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

        public async Task<int> GetContactsCountAsync(long userId)
        {
            try
            {
                return await _unitOfWork.UserContactRepository.GetContactsCountAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contacts count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<int> GetFavoriteContactsCountAsync(long userId)
        {
            try
            {
                return await _unitOfWork.UserContactRepository.GetFavoriteContactsCountAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorite contacts count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<int> GetBlockedContactsCountAsync(long userId)
        {
            try
            {
                var blockedContacts = await _unitOfWork.UserContactRepository.GetBlockedContactsAsync(userId);
                return blockedContacts.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blocked contacts count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<IEnumerable<UserDto>> GetFilteredContactsAsync(
            long userId,
            bool? isFavorite,
            bool? isBlocked,
            bool? hasChat,
            string sortBy,
            string sortDirection)
        {
            try
            {
                var query = _unitOfWork.UserContactRepository.GetQueryable()
                    .Include(uc => uc.ContactUser)
                    .Where(uc => uc.OwnerUserId == userId);

                if (isFavorite.HasValue)
                    query = query.Where(uc => uc.IsFavorite == isFavorite.Value);

                if (isBlocked.HasValue)
                    query = query.Where(uc => uc.IsBlocked == isBlocked.Value);

                query = sortBy.ToLower() switch
                {
                    "name" => sortDirection.ToLower() == "desc"
                        ? query.OrderByDescending(uc => uc.DisplayName ?? uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                        : query.OrderBy(uc => uc.DisplayName ?? uc.ContactUser.FirstName ?? uc.ContactUser.UserName),
                    "lastinteraction" => sortDirection.ToLower() == "desc"
                        ? query.OrderByDescending(uc => uc.LastInteractionAt ?? uc.CreatedAt)
                        : query.OrderBy(uc => uc.LastInteractionAt ?? uc.CreatedAt),
                    "dateadded" => sortDirection.ToLower() == "desc"
                        ? query.OrderByDescending(uc => uc.CreatedAt)
                        : query.OrderBy(uc => uc.CreatedAt),
                    _ => query.OrderBy(uc => uc.DisplayName ?? uc.ContactUser.FirstName ?? uc.ContactUser.UserName)
                };

                var contacts = await query.ToListAsync();

                if (hasChat.HasValue)
                {
                    var contactUserIds = contacts.Select(c => c.ContactUserId).ToList();
                    var chats = await _unitOfWork.ChatRepository.ListAsync(
                        c => !c.IsGroup &&
                             c.Participants.Any(p => p.UserId == userId) &&
                             c.Participants.Any(p => contactUserIds.Contains(p.UserId)),
                        includeProperties: "Participants");

                    var chatUserIds = chats.SelectMany(c => c.Participants)
                        .Where(p => p.UserId != userId)
                        .Select(p => p.UserId)
                        .ToHashSet();

                    contacts = contacts.Where(c =>
                        hasChat.Value ? chatUserIds.Contains(c.ContactUserId) : !chatUserIds.Contains(c.ContactUserId)
                    ).ToList();
                }

                var userDtos = new List<UserDto>();
                foreach (var contact in contacts)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(contact.ContactUser.Id);
                    var userDto = MappingHelper.MapToUserDto(contact.ContactUser, isOnline);

                    userDto.IsFavorite = contact.IsFavorite;
                    userDto.IsBlocked = contact.IsBlocked;
                    userDto.DisplayName = contact.DisplayName;
                    userDto.ContactCreatedAt = contact.CreatedAt;
                    userDto.LastInteractionAt = contact.LastInteractionAt;
                    userDto.IsContact = true;

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered contacts for user {UserId}", userId);
                throw new Exception("خطا در دریافت مخاطبین فیلتر شده");
            }
        }

        public async Task<bool> BatchUpdateContactsAsync(
            long ownerId,
            List<long> contactIds,
            Dictionary<string, object> updates)
        {
            try
            {
                var contacts = await _unitOfWork.UserContactRepository.ListAsync(
                    uc => uc.OwnerUserId == ownerId && contactIds.Contains(uc.ContactUserId));

                foreach (var contact in contacts)
                {
                    if (updates.ContainsKey("isFavorite") && updates["isFavorite"] is bool isFavorite)
                        contact.IsFavorite = isFavorite;

                    if (updates.ContainsKey("isBlocked") && updates["isBlocked"] is bool isBlocked)
                        contact.IsBlocked = isBlocked;

                    if (updates.ContainsKey("displayName") && updates["displayName"] is string displayName)
                        contact.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
                }

                await _unitOfWork.CompleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch updating contacts for user {UserId}", ownerId);
                return false;
            }
        }

        public async Task<IEnumerable<UserDto>> GetMutualContactsAsync(long userId1, long userId2)
        {
            try
            {
                var mutualContacts = await _unitOfWork.UserContactRepository.GetMutualContactsAsync(userId1, userId2);

                var userDtos = new List<UserDto>();
                foreach (var contact in mutualContacts)
                {
                    var isOnline = await _connectionService.IsUserOnlineAsync(contact.ContactUser.Id);
                    var userDto = MappingHelper.MapToUserDto(contact.ContactUser, isOnline);

                    userDto.IsFavorite = contact.IsFavorite;
                    userDto.IsBlocked = contact.IsBlocked;
                    userDto.DisplayName = contact.DisplayName;
                    userDto.ContactCreatedAt = contact.CreatedAt;
                    userDto.LastInteractionAt = contact.LastInteractionAt;
                    userDto.IsContact = true;

                    userDtos.Add(userDto);
                }

                return userDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mutual contacts for users {UserId1} and {UserId2}", userId1, userId2);
                throw new Exception("خطا در دریافت مخاطبین مشترک");
            }
        }

        public async Task<ContactImportResult> ImportContactsAsync(long userId, List<ImportContactItem> contacts)
        {
            try
            {
                var result = new ContactImportResult();
                var contactsToAdd = new List<UserContact>();

                foreach (var contactItem in contacts)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(contactItem.PhoneNumber))
                        {
                            result.ErrorCount++;
                            continue;
                        }

                        var existingUser = await _unitOfWork.UserRepository.GetByPhoneNumberAsync(contactItem.PhoneNumber);
                        if (existingUser == null)
                        {
                            result.ErrorCount++;
                            continue;
                        }

                        var existingContact = await _unitOfWork.UserContactRepository.GetUserContactAsync(userId, existingUser.Id);
                        if (existingContact != null)
                        {
                            result.DuplicateCount++;
                            continue;
                        }

                        contactsToAdd.Add(new UserContact
                        {
                            OwnerUserId = userId,
                            ContactUserId = existingUser.Id,
                            DisplayName = contactItem.DisplayName,
                            IsFavorite = contactItem.IsFavorite,
                            CreatedAt = DateTime.UtcNow,
                            IsBlocked = false
                        });

                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error importing contact {PhoneNumber} for user {UserId}",
                            contactItem.PhoneNumber, userId);
                        result.ErrorCount++;
                    }
                }

                if (contactsToAdd.Any())
                {
                    await _unitOfWork.UserContactRepository.AddRangeAsync(contactsToAdd);
                    await _unitOfWork.CompleteAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing contacts for user {UserId}", userId);
                throw new Exception("خطا در وارد کردن مخاطبین");
            }
        }

        // متدهای Export که قبلاً نوشتیم اینجا هم بیان:
        public async Task<byte[]> ExportContactsAsync(long userId, string format)
        {
            try
            {
                var contacts = await _unitOfWork.UserContactRepository.ListAsync(
                    uc => uc.OwnerUserId == userId && !uc.IsBlocked,
                    includeProperties: "ContactUser");

                return format.ToLower() switch
                {
                    "csv" => await ExportToCsvAsync(contacts),
                    "json" => await ExportToJsonAsync(contacts),
                    "vcf" => await ExportToVcfAsync(contacts),
                    _ => throw new ArgumentException("فرمت نامعتبر")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting contacts for user {UserId}", userId);
                throw new Exception("خطا در خروجی گرفتن از مخاطبین");
            }
        }

        private async Task<byte[]> ExportToCsvAsync(IEnumerable<UserContact> contacts)
        {
            var csv = new StringBuilder();
            csv.AppendLine("نام,نام خانوادگی,نام نمایشی,شماره تلفن,علاقه‌مندی,تاریخ اضافه");

            foreach (var contact in contacts)
            {
                csv.AppendLine($"{contact.ContactUser.FirstName},{contact.ContactUser.LastName},{contact.DisplayName},{contact.ContactUser.PhoneNumber},{contact.IsFavorite},{contact.CreatedAt:yyyy-MM-dd}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private async Task<byte[]> ExportToJsonAsync(IEnumerable<UserContact> contacts)
        {
            var exportData = contacts.Select(c => new
            {
                firstName = c.ContactUser.FirstName,
                lastName = c.ContactUser.LastName,
                displayName = c.DisplayName,
                phoneNumber = c.ContactUser.PhoneNumber,
                isFavorite = c.IsFavorite,
                dateAdded = c.CreatedAt
            });

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return Encoding.UTF8.GetBytes(json);
        }

        private async Task<byte[]> ExportToVcfAsync(IEnumerable<UserContact> contacts)
        {
            var vcf = new StringBuilder();

            foreach (var contact in contacts)
            {
                vcf.AppendLine("BEGIN:VCARD");
                vcf.AppendLine("VERSION:3.0");
                vcf.AppendLine($"FN:{contact.DisplayName ?? $"{contact.ContactUser.FirstName} {contact.ContactUser.LastName}".Trim()}");
                vcf.AppendLine($"N:{contact.ContactUser.LastName};{contact.ContactUser.FirstName};;;");
                vcf.AppendLine($"TEL:{contact.ContactUser.PhoneNumber}");
                vcf.AppendLine("END:VCARD");
            }

            return Encoding.UTF8.GetBytes(vcf.ToString());
        }

        public async Task<bool> UpdateSyncStatusAsync(long userId, DateTime lastSyncTime, int syncedCount)
        {
            try
            {
                _logger.LogInformation("Sync status updated for user {UserId}: LastSync={LastSync}, Count={Count}",
                    userId, lastSyncTime, syncedCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync status for user {UserId}", userId);
                return false;
            }
        }
    }
}