using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Infrastructure.Services;



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
            var usersFromRepo = await _unitOfWork.UserRepository.FindUsersByPhoneNumbersAsync(phoneNumbers);

            var filteredUsers = usersFromRepo.Where(u => u.Id != currentUserId);

            var userDtos = new List<UserDto>();
            foreach (var user in filteredUsers)
            {
                var isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                userDtos.Add(MappingHelper.MapToUserDto(user, isOnline));
            }
            return userDtos;
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
                await _unitOfWork.CompleteAsync(); // ذخیره تغییرات در دیتابیس
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
    }
}