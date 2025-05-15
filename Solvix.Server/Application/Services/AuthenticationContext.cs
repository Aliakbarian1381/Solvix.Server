using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.Application.Services
{
    public class AuthenticationContext
    {
        private readonly IEnumerable<IAuthenticationStrategy> _strategies;
        private readonly ILogger<AuthenticationContext> _logger;

        public AuthenticationContext(IEnumerable<IAuthenticationStrategy> strategies, ILogger<AuthenticationContext> logger)
        {
            _strategies = strategies;
            _logger = logger;
        }

        public async Task<AppUser?> AuthenticateAsync(object credentials)
        {
            var credentialType = credentials.GetType();
            var strategy = _strategies.FirstOrDefault(s => s.SupportsCredentialType(credentialType));

            if (strategy == null)
            {
                _logger.LogWarning("هیچ استراتژی احراز هویتی برای نوع اعتبارنامه {CredentialType} یافت نشد", credentialType.Name);
                return null;
            }

            return await strategy.AuthenticateAsync(credentials);
        }
    }
}
