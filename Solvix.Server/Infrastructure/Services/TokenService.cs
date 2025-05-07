using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;

namespace Solvix.Server.Infrastructure.Services
{

    public class TokenService : ITokenService
    {
        private readonly SymmetricSecurityKey _key;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly TimeSpan _tokenLifetime;

        public TokenService(IConfiguration configuration)
        {
            var jwtKey = configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key not configured in appsettings.json");
            }
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var issuerValue = configuration["Jwt:Issuer"];
            if (string.IsNullOrEmpty(issuerValue))
            {
                throw new InvalidOperationException("JWT Issuer not configured in appsettings.json");
            }
            _issuer = issuerValue;

            var audienceValue = configuration["Jwt:Audience"];
            if (string.IsNullOrEmpty(audienceValue))
            {
                throw new InvalidOperationException("JWT Audience not configured in appsettings.json");
            }
            _audience = audienceValue;

            _tokenLifetime = TimeSpan.FromDays(7);
        }


        public string CreateToken(AppUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
            }

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(_tokenLifetime),
                SigningCredentials = creds,
                Issuer = _issuer,
                Audience = _audience
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        public ClaimsPrincipal? ValidateTokenWithoutLifetime(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _issuer,
                    ValidAudience = _audience,
                    IssuerSigningKey = _key,
                    ValidateLifetime = false
                }, out _);

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}