using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Solvix.Server.Data;
using Solvix.Server.Dtos;
using System.Security.Claims;
using Solvix.Server.Helpers;

namespace Solvix.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : BaseController
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<UserController> _logger;

        public UserController(ChatDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [HttpGet("search")]
        public async Task<ActionResult<List<UserDto>>> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new List<UserDto>());
            }

            var trimmedQuery = query.Trim();

            long currentUserId;
            try
            {
                currentUserId = GetUserId();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to search users: {Message}", ex.Message);
                return Unauthorized("User ID could not be determined.");
            }


            try
            {
                var users = await _context.Users
                    .Where(u => u.Id != currentUserId &&
                           (EF.Functions.Like(u.FirstName, $"%{trimmedQuery}%") ||
                            EF.Functions.Like(u.LastName, $"%{trimmedQuery}%") ||
                            (u.PhoneNumber != null && u.PhoneNumber.Contains(trimmedQuery))))
                    .Select(u => UserMappingHelper.MapAppUserToUserDto(u))
                    .Take(20)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching users with query: {Query}", trimmedQuery);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
    }
}