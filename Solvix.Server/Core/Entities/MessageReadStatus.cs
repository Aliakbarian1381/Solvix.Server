using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class MessageReadStatus
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        [Required]
        public long ReaderId { get; set; }

        public DateTime ReadAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("MessageId")]
        public virtual Message Message { get; set; } = null!;

        [ForeignKey("ReaderId")]
        public virtual AppUser Reader { get; set; } = null!;
    }
}
