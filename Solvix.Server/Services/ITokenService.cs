using Solvix.Server.Models;

namespace Solvix.Server.Services
{
    public interface ITokenService
    {
        string CreateToken(AppUser user);

    }
}
