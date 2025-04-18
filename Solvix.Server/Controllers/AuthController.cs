using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Solvix.Server.Dtos;
using Solvix.Server.Models;
using Solvix.Server.Services;
using System.Security.Claims;
using System.Threading.Channels;

namespace Solvix.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;

        public AuthController(UserManager<AppUser> userManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
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
                Console.WriteLine(e.Message);
                return Ok(new { exists = "" != null });
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

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Token = _tokenService.CreateToken(user)
            });
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

                return Ok(new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Token = _tokenService.CreateToken(user)
                });
            }
            catch (Exception ex)
            {

                throw;
            }
            
        }

        [HttpGet("currentuser")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userIdString);
            if (user == null) return NotFound(new { message = "کاربر یافت نشد" });

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName
            });
        }
    }
}
