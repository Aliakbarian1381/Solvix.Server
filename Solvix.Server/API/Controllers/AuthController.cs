using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Application.Helpers;
using Solvix.Server.Application.Services;
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
        private readonly IUserConnectionService _connectionService;
        private readonly IOtpService _otpService;
        private readonly AuthenticationContext _authContext;


        public AuthController(
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            IUserService userService,
            IUserConnectionService connectionService,
            IOtpService otpService,
            AuthenticationContext authContext,
            ILogger<AuthController> logger) : base(logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _userService = userService;
            _otpService = otpService;
            _authContext = authContext;
            _connectionService = connectionService;
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

                var userDto = MappingHelper.MapToUserDto(user, false, _tokenService.CreateToken(user));
                return Ok(userDto, "ثبت نام با موفقیت انجام شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for phone number {PhoneNumber}", registerDto.PhoneNumber);
                return ServerError("خطا در فرآیند ثبت نام");
            }
        }

        [EnableRateLimiting("OtpRequestLimit")]
        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp(OtpRequestDto request)
        {
            try
            {
                _logger.LogInformation("درخواست کد OTP برای شماره: {PhoneNumber}", request.PhoneNumber);

                // بررسی وجود کاربر با این شماره
                var user = await _userManager.FindByNameAsync(request.PhoneNumber);
                bool userExists = user != null;

                // تولید و ارسال کد OTP
                await _otpService.GenerateOtpAsync(request.PhoneNumber);

                return Ok(new { success = true, message = "کد تایید ارسال شد", userExists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در درخواست کد OTP برای شماره {PhoneNumber}", request.PhoneNumber);
                return ServerError("خطا در ارسال کد تایید");
            }
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp(OtpVerifyDto request)
        {
            try
            {
                _logger.LogInformation("تلاش برای تایید کد OTP برای شماره: {PhoneNumber}", request.PhoneNumber);

                // بررسی وجود کاربر
                var user = await _userManager.FindByNameAsync(request.PhoneNumber);
                if (user == null)
                {
                    _logger.LogWarning("تلاش برای تایید OTP برای شماره غیرموجود: {PhoneNumber}", request.PhoneNumber);
                    return Unauthorized(new { message = "شماره تلفن یافت نشد" });
                }

                // استفاده از استراتژی OTP برای احراز هویت
                var credentials = new OtpVerifyDto
                {
                    PhoneNumber = request.PhoneNumber,
                    OtpCode = request.OtpCode
                };

                var authenticatedUser = await _authContext.AuthenticateAsync(credentials);

                if (authenticatedUser == null)
                {
                    _logger.LogWarning("تایید OTP ناموفق برای شماره: {PhoneNumber}", request.PhoneNumber);
                    return Unauthorized(new { message = "کد تایید نامعتبر است" });
                }

                user.LastActiveAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                bool isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                var userDto = MappingHelper.MapToUserDto(user, isOnline, _tokenService.CreateToken(user));

                _logger.LogInformation("تایید OTP موفق برای کاربر {UserId} با شماره {PhoneNumber}", user.Id, request.PhoneNumber);
                return Ok(userDto, "ورود با موفقیت انجام شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تایید کد OTP برای شماره {PhoneNumber}", request.PhoneNumber);
                return ServerError("خطا در تایید کد");
            }
        }

        [HttpPost("register-with-otp")]
        public async Task<IActionResult> RegisterWithOtp(OtpRegisterDto request)
        {
            try
            {
                _logger.LogInformation("تلاش برای ثبت‌نام با OTP برای شماره: {PhoneNumber}", request.PhoneNumber);

                if (await _userService.CheckPhoneExistsAsync(request.PhoneNumber))
                {
                    _logger.LogWarning("تلاش برای ثبت‌نام با شماره تکراری: {PhoneNumber}", request.PhoneNumber);
                    return BadRequest("این شماره تلفن قبلا ثبت شده است.");
                }

                // تایید کد OTP
                var isOtpValid = await _otpService.ValidateOtpAsync(request.PhoneNumber, request.OtpCode);
                if (!isOtpValid)
                {
                    _logger.LogWarning("کد OTP نامعتبر در هنگام ثبت‌نام برای شماره: {PhoneNumber}", request.PhoneNumber);
                    return Unauthorized(new { message = "کد تایید نامعتبر است" });
                }

                // تولید یک رمز عبور تصادفی برای کاربر - کاربر هرگز از آن استفاده نخواهد کرد
                string randomPassword = Guid.NewGuid().ToString("N").Substring(0, 12) + "Aa1!";

                var user = new AppUser
                {
                    UserName = request.PhoneNumber,
                    FirstName = request.FirstName ?? "",
                    LastName = request.LastName ?? "",
                    PhoneNumber = request.PhoneNumber,
                    PhoneNumberConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, randomPassword);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    _logger.LogError("خطا در ایجاد کاربر جدید با OTP: {Errors}", string.Join(", ", errors));
                    return BadRequest(string.Join(", ", errors));
                }

                var userDto = MappingHelper.MapToUserDto(user, false, _tokenService.CreateToken(user));
                _logger.LogInformation("ثبت‌نام موفق با OTP برای کاربر جدید با شناسه {UserId}", user.Id);
                return Ok(userDto, "ثبت نام با موفقیت انجام شد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ثبت‌نام با OTP برای شماره {PhoneNumber}", request.PhoneNumber);
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

                user.LastActiveAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                bool isOnline = await _connectionService.IsUserOnlineAsync(user.Id);
                var userDto = MappingHelper.MapToUserDto(user, isOnline, _tokenService.CreateToken(user));

                return Ok(userDto, "ورود با موفقیت انجام شد.");
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

                await _userService.UpdateUserLastActiveAsync(userId);
                bool isOnline = await _connectionService.IsUserOnlineAsync(userId);
                var userDto = MappingHelper.MapToUserDto(user, isOnline, _tokenService.CreateToken(user));
                return base.Ok(userDto);
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