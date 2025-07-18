using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class Participant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        [Required]
        public long UserId { get; set; }

        public string Role { get; set; } = "Member"; // Member, Admin, Owner

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }
}
