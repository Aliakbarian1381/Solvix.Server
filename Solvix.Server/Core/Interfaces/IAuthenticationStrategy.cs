using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IAuthenticationStrategy
    {
        Task<AppUser?> AuthenticateAsync(object credentials);
        bool SupportsCredentialType(Type credentialType);
    }
}
