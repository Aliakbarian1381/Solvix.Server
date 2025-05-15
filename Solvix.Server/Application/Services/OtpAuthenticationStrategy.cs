using Microsoft.AspNetCore.Identity;
using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.Application.Services
{
    public class OtpAuthenticationStrategy : IAuthenticationStrategy
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IOtpService _otpService;

        public OtpAuthenticationStrategy(UserManager<AppUser> userManager, IOtpService otpService)
        {
            _userManager = userManager;
            _otpService = otpService;
        }

        public async Task<AppUser?> AuthenticateAsync(object credentials)
        {
            if (credentials is not OtpVerifyDto otpDto) return null;

            var user = await _userManager.FindByNameAsync(otpDto.PhoneNumber);
            if (user == null) return null;

            var isOtpValid = await _otpService.ValidateOtpAsync(user.PhoneNumber, otpDto.OtpCode);
            return isOtpValid ? user : null;
        }

        public bool SupportsCredentialType(Type credentialType)
        {
            return credentialType == typeof(OtpVerifyDto);
        }
    }
}
