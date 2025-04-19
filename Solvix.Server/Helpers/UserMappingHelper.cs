using Solvix.Server.Dtos;
using Solvix.Server.Models; 

namespace Solvix.Server.Helpers
{
    public static class UserMappingHelper
    {
        public static UserDto MapAppUserToUserDto(AppUser user, string? token = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "AppUser cannot be null for mapping.");
            }

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName ?? "", 
                FirstName = user.FirstName,
                LastName = user.LastName,
                Token = token ?? "" 
            };
        }

        public static UserDto MapAppUserToUserDto(AppUser user)
        {
            return MapAppUserToUserDto(user, null);
        }
    }
}