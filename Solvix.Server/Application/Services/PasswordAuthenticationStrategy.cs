using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.Application.Services
{
    public class PasswordAuthenticationStrategy : IAuthenticationStrategy
    {
        private readonly UserManager<AppUser> _userManager;

        public PasswordAuthenticationStrategy(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<AppUser?> AuthenticateAsync(object credentials)
        {
            if (credentials is not LoginDto loginDto) return null;

            var user = await _userManager.FindByNameAsync(loginDto.PhoneNumber);
            if (user == null) return null;

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
            return isPasswordValid ? user : null;
        }

        public bool SupportsCredentialType(Type credentialType)
        {
            return credentialType == typeof(LoginDto);
        }
    }
}
