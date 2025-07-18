using Microsoft.AspNetCore.Identity;

namespace Solvix.Server.Core.Entities
{
    public class AppRole : IdentityRole<long>
    {
        public AppRole() : base() { }

        public AppRole(string roleName) : base(roleName) { }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? Description { get; set; }
    }
}
