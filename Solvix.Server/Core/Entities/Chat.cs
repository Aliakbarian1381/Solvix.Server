using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class Chat
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsGroup { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public long? OwnerId { get; set; } // Nullable for non-group chats
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }


        // Navigation Properties
        public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual GroupSettings? GroupSettings { get; set; }
    }
}