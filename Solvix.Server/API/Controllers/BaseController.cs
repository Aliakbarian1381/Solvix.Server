using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Solvix.Server.API.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        protected readonly ILogger<BaseController> _logger;

        protected BaseController(ILogger<BaseController> logger)
        {
            _logger = logger;
        }

        protected long GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            _logger.LogWarning("Failed to get user ID from claims");
            throw new UnauthorizedAccessException("User ID could not be determined from claims.");
        }

        protected string GetUsername()
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Failed to get username from claims");
                throw new UnauthorizedAccessException("Username could not be determined from claims.");
            }

            return username;
        }

        protected IActionResult Ok<T>(T data, string? message = null)
        {
            return base.Ok(new { success = true, message, data });
        }

        protected IActionResult BadRequest(string message)
        {
            return base.BadRequest(new { success = false, message });
        }

        protected IActionResult ServerError(string message = "An unexpected error occurred.")
        {
            return StatusCode(500, new { success = false, message });
        }

        protected IActionResult Forbidden(string message = "You don't have permission to access this resource.")
        {
            return StatusCode(403, new { success = false, message });
        }
    }
}