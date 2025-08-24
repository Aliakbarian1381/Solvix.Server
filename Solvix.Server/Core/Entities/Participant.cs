using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Solvix.Server.Core.Entities
{
    public class Participant
    {
        [Key]
        public int Id { get; set; }

        public Guid ChatId { get; set; }

        public long UserId { get; set; }

        public GroupRole Role { get; set; } = GroupRole.Member; // Member, Admin, Owner

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual Chat Chat { get; set; } = null!;
        public virtual AppUser User { get; set; } = null!;
    }
}
