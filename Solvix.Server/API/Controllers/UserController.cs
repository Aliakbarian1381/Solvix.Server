using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;
using System.Security.Claims;

namespace Solvix.Server.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : BaseController
    {
        private readonly IUserService _userService;
        private readonly IUserConnectionService _connectionService;

        public UserController(
            IUserService userService,
            IUserConnectionService connectionService,
            ILogger<UserController> logger) : base(logger)
        {
            _userService = userService;
            _connectionService = connectionService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new List<UserDto>());
                }

                var currentUserId = GetUserId();
                var userDtos = await _userService.SearchUsersAsync(query, currentUserId);

                return Ok(userDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to search users");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with query: {Query}", query);
                return ServerError("خطا در جستجوی کاربران");
            }
        }

        [HttpPost("sync-contacts")]
        public async Task<IActionResult> SyncContacts([FromBody] List<string> phoneNumbers)
        {
            try
            {
                var currentUserId = GetUserId();
                var users = await _userService.FindUsersByPhoneNumbersAsync(phoneNumbers, currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing contacts for user {UserId}", GetUserId());
                return ServerError("خطا در همگام‌سازی مخاطبین");
            }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUser(long userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "کاربر یافت نشد" });
                }

                bool isOnline = await _connectionService.IsUserOnlineAsync(userId);
                var userDto = MappingHelper.MapToUserDto(user, isOnline);

                return Ok(userDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get user {UserId}", userId);
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", userId);
                return ServerError("خطا در دریافت اطلاعات کاربر");
            }
        }

        [HttpGet("saved-contacts")]
        public async Task<IActionResult> GetSavedContacts()
        {
            try
            {
                var currentUserId = GetUserId();
                var users = await _userService.GetSavedContactsAsync(currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین ذخیره شده");
            }
        }

        [HttpGet("saved-contacts-with-chat")]
        public async Task<IActionResult> GetSavedContactsWithChat()
        {
            try
            {
                var currentUserId = GetUserId();
                var users = await _userService.GetSavedContactsWithChatInfoAsync(currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved contacts with chat info for user {UserId}", GetUserId());
                return ServerError("خطا در دریافت مخاطبین با اطلاعات چت");
            }
        }

        [HttpGet("online")]
        public async Task<IActionResult> GetOnlineUsers()
        {
            try
            {
                var onlineAppUsers = await _connectionService.GetOnlineUsersAsync();

                var userDtos = onlineAppUsers
                    .Select(user => MappingHelper.MapToUserDto(user, true))
                    .ToList();

                return Ok(userDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get online users");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online users");
                return ServerError("خطا در دریافت کاربران آنلاین");
            }
        }

        [HttpPost("update-fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] FcmTokenDto tokenDto)
        {
            if (tokenDto == null || string.IsNullOrWhiteSpace(tokenDto.Token))
            {
                return BadRequest(new { message = "FCM token cannot be empty." });
            }

            try
            {
                var userId = GetUserId();
                var result = await _userService.UpdateFcmTokenAsync(userId, tokenDto.Token);

                if (result)
                {
                    return Ok(new { message = "FCM token updated successfully." });
                }
                return ServerError("Failed to update FCM token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateFcmToken endpoint for user {UserId}", GetUserId());
                return ServerError("An unexpected error occurred while updating the token.");
            }
        }

        [HttpPost("contacts/{contactId}/favorite")]
        public async Task<IActionResult> SetContactFavorite(long contactId, [FromBody] bool isFavorite)
        {
            try
            {
                var currentUserId = GetUserId();
                var result = await _userService.MarkContactAsFavoriteAsync(currentUserId, contactId, isFavorite);

                if (result)
                {
                    return Ok(new { message = isFavorite ? "مخاطب به علاقه‌مندی‌ها اضافه شد" : "مخاطب از علاقه‌مندی‌ها حذف شد" });
                }
                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting contact {ContactId} favorite status for user {UserId}", contactId, GetUserId());
                return ServerError("خطا در تنظیم وضعیت علاقه‌مندی مخاطب");
            }
        }

        [HttpPost("contacts/{contactId}/block")]
        public async Task<IActionResult> BlockContact(long contactId, [FromBody] bool isBlocked)
        {
            try
            {
                var currentUserId = GetUserId();
                var result = await _userService.BlockContactAsync(currentUserId, contactId, isBlocked);

                if (result)
                {
                    return Ok(new { message = isBlocked ? "مخاطب مسدود شد" : "مخاطب از حالت مسدود خارج شد" });
                }
                return NotFound(new { message = "مخاطب یافت نشد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking contact {ContactId} for user {UserId}", contactId, GetUserId());
                return ServerError("خطا در مسدود کردن مخاطب");
            }
        }

        [HttpPost("update-last-active")]
        public async Task<IActionResult> UpdateLastActive()
        {
            try
            {
                var userId = GetUserId();
                var result = await _userService.UpdateUserLastActiveAsync(userId);

                if (result)
                {
                    return Ok(new { message = "Last active time updated successfully." });
                }
                return ServerError("Failed to update last active time.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last active time for user {UserId}", GetUserId());
                return ServerError("An unexpected error occurred while updating last active time.");
            }
        }
    }
}