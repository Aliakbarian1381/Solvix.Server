using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class GroupMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        public GroupRole Role { get; set; } = GroupRole.Member;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
