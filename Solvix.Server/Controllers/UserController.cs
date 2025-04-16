using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using System.Security.Claims;

namespace Solvix.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly ChatDbContext _context;

        public UserController(ChatDbContext context)
        {
            _context = context;
        }

        private long GetUserId() =>
            long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            var currentUserId = GetUserId();

            var users = await _context.Users
                .Where(u => u.Id != currentUserId &&
                    (u.FirstName.ToLower().Contains(query.ToLower()) ||
                     u.LastName.ToLower().Contains(query.ToLower()) ||
                     u.PhoneNumber!.Contains(query)))
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber
                })
                .Take(20)
                .ToListAsync();

            return Ok(users);
        }
    }
}
