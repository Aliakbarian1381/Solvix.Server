using Microsoft.AspNetCore.Identity;

namespace Solvix.Server.Core.Entities
{
    public class AppRole : IdentityRole<long>
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Constructor
        public AppRole() : base()
        {
        }

        public AppRole(string roleName) : base(roleName)
        {
            Name = roleName;
        }

        // Predefined roles
        public static class Roles
        {
            public const string Admin = "Admin";
            public const string User = "User";
            public const string Moderator = "Moderator";
        }
    }
}