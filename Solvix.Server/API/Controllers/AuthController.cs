using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.API.Controllers
{
    [EnableRateLimiting("AuthLimit")]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IUserService _userService;

        public AuthController(
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            IUserService userService,
            ILogger<AuthController> logger) : base(logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _userService = userService;
        }

        [HttpGet("check-phone/{phoneNumber}")]
        public async Task<IActionResult> CheckPhone(string phoneNumber)
        {
            try
            {
                _logger.LogInformation("Received check-phone request for: {PhoneNumber}", phoneNumber);

                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    return BadRequest("شماره تلفن نمیتواند خالی باشد!");
                }

                var exists = await _userService.CheckPhoneExistsAsync(phoneNumber);
                _logger.LogInformation("Phone check result for {PhoneNumber}: {Exists}", phoneNumber, exists);

                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking phone number {PhoneNumber}", phoneNumber);
                return ServerError("خطا در بررسی شماره تلفن");
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            try
            {
                if (await _userService.CheckPhoneExistsAsync(registerDto.PhoneNumber))
                {
                    return BadRequest("این شماره تلفن قبلا ثبت شده است.");
                }

                var user = new AppUser
                {
                    UserName = registerDto.PhoneNumber,
                    FirstName = registerDto.FirstName ?? "",
                    LastName = registerDto.LastName ?? "",
                    PhoneNumber = registerDto.PhoneNumber,
                    PhoneNumberConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    return BadRequest(string.Join(", ", errors));
                }

                var userDto = MappingHelper.MapToUserDto(user, _tokenService.CreateToken(user));
                return Ok(userDto, "ثبت نام با موفقیت انجام شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for phone number {PhoneNumber}", registerDto.PhoneNumber);
                return ServerError("خطا در فرآیند ثبت نام");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(loginDto.PhoneNumber);
                if (user == null)
                {
                    return Unauthorized(new { message = "شماره تلفن یا رمز عبور نامعتبر است" });
                }

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
                if (!isPasswordValid)
                {
                    return Unauthorized(new { message = "شماره تلفن یا رمز عبور نامعتبر است" });
                }

                // بروزرسانی آخرین زمان فعالیت
                user.LastActiveAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                var userDto = MappingHelper.MapToUserDto(user, _tokenService.CreateToken(user));

                return Ok(new ApiResponse<UserDto>
                {
                    Success = true,
                    Message = "ورود با موفقیت انجام شد.",
                    Data = userDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for phone number {PhoneNumber}", loginDto.PhoneNumber);
                return ServerError("خطا در فرآیند ورود");
            }
        }

        [HttpGet("current-user")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                long userId = GetUserId();
                var user = await _userManager.FindByIdAsync(userId.ToString());

                if (user == null)
                {
                    return NotFound(new { message = "کاربر یافت نشد" });
                }

                // بروزرسانی آخرین زمان فعالیت
                await _userService.UpdateUserLastActiveAsync(userId);

                var userDto = MappingHelper.MapToUserDto(user);
                return Ok(userDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get current user");
                return Unauthorized(new { message = "احراز هویت ناموفق بود" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return ServerError("خطا در دریافت اطلاعات کاربر");
            }
        }

        [HttpGet("refresh-token")]
        [Authorize]
        public async Task<IActionResult> RefreshToken()
        {
            try
            {
                long userId = GetUserId();
                var user = await _userManager.FindByIdAsync(userId.ToString());

                if (user == null)
                {
                    return NotFound(new { message = "کاربر یافت نشد" });
                }

                // صدور توکن جدید
                var token = _tokenService.CreateToken(user);

                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return ServerError("خطا در بروزرسانی توکن");
            }
        }
    }
}