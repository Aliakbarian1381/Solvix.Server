using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Solvix.Server.Controllers
{
    [ApiController]
    public class BaseController : ControllerBase
    {
        public BaseController()
        {
        }

        protected long GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("User ID could not be determined from claims.");
        }


        protected async Task<bool> IsUserParticipant(ChatDbContext context, Guid chatId, long userId)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), "ChatDbContext cannot be null when checking user participant status.");
            }
            return await context.ChatParticipants
                .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);
        }

    }
}