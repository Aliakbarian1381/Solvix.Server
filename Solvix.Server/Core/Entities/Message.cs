using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class Message
    {
        [Key]
        public int Id { get; set; }
        public long SenderId { get; set; }
        public Guid ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // Message status properties  
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation Properties
        public virtual AppUser Sender { get; set; } = null!;
        public virtual Chat Chat { get; set; } = null!;
        public virtual ICollection<MessageReadStatus> ReadStatuses { get; set; } = new List<MessageReadStatus>();

    }
}