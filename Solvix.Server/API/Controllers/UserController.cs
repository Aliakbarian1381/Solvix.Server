using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
                    return Ok(new List<object>());
                }

                var currentUserId = GetUserId();
                var users = await _userService.SearchUsersAsync(query, currentUserId);

                // اضافه کردن وضعیت آنلاین بودن به نتایج جستجو
                foreach (var user in users)
                {
                    user.IsOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                }

                return Ok(users);
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

                var userDto = MappingHelper.MapToUserDto(user, await _connectionService.IsUserOnlineAsync(userId));


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
                var onlineUsers = await _connectionService.GetOnlineUsersAsync();

                var userDtos = onlineUsers
                    .Select(user => MappingHelper.MapToUserDto(user))
                    .ToList();

                foreach (var user in userDtos)
                {
                    user.IsOnline = true;
                }

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
    }
}