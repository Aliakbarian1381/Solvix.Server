using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Solvix.Server.Dtos;
using Solvix.Server.Models;
using Solvix.Server.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging; 
using Solvix.Server.Helpers;

namespace Solvix.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<AppUser> userManager, ITokenService tokenService, ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpGet("check-phone/{phoneNumber}")]
        public async Task<IActionResult> CheckPhone(string phoneNumber)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(phoneNumber);
                return Ok(new { exists = user != null });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error checking phone number {PhoneNumber}", phoneNumber);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while checking the phone number." });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            var existingUser = await _userManager.FindByNameAsync(registerDto.PhoneNumber);
            if (existingUser != null)
                return BadRequest(new { message = "این شماره تلفن قبلا ثبت شده است." });

            var user = new AppUser
            {
                UserName = registerDto.PhoneNumber,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                PhoneNumber = registerDto.PhoneNumber,
                PhoneNumberConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            return Ok(UserMappingHelper.MapAppUserToUserDto(user, _tokenService.CreateToken(user)));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(loginDto.PhoneNumber);
                if (user == null)
                    return Unauthorized(new { message = "شماره تلفن یا رمز عبور نامعتبر است" });

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
                if (!isPasswordValid)
                    return Unauthorized(new { message = "شماره تلفن یا رمز عبور نامعتبر است" });

                return Ok(UserMappingHelper.MapAppUserToUserDto(user, _tokenService.CreateToken(user)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for phone number {PhoneNumber}", loginDto.PhoneNumber); // استفاده از logger
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during login." });
            }
        }

        [HttpGet("currentuser")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            long userId;
            try
            {
                userId = GetUserId();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to get current user: {Message}", ex.Message);
                return Unauthorized("User ID could not be determined.");
            }


            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound(new { message = "کاربر یافت نشد" });

            return Ok(UserMappingHelper.MapAppUserToUserDto(user));
        }
    }
}