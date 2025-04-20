using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;



namespace Solvix.Server.Application.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<AppUser> userManager,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
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

                // حذف کاربر فعلی از نتایج جستجو
                users = users.Where(u => u.Id != currentUserId).ToList();

                return users.Select(user => MappingHelper.MapToUserDto(user)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term: {SearchTerm}", searchTerm);
                return new List<UserDto>();
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