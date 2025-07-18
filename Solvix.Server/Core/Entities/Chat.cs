using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class Chat
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsGroup { get; set; } = false;

        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? AvatarUrl { get; set; }

        public long? OwnerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public virtual GroupSettings? GroupSettings { get; set; }
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public virtual GroupSettings? GroupSettings { get; set; }
    }

}
