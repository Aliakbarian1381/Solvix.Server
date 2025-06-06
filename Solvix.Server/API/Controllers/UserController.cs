using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
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

        [Authorize]
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
    }
}