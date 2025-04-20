using Solvix.Server.Core.Entities;
using System.Security.Claims;

namespace Solvix.Server.Core.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(AppUser user);
        ClaimsPrincipal? ValidateTokenWithoutLifetime(string token);
    }

}
