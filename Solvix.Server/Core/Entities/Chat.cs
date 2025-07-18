namespace Solvix.Server.Core.Entities
{
    public class Chat
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? GroupImageUrl { get; set; } // همونطور که در migration هست
        public bool IsGroup { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastMessageTime { get; set; }
        public string? LastMessage { get; set; }
        public int UnreadCount { get; set; } = 0;

        // Group properties - مطابق با migration
        public long? OwnerId { get; set; }
        public int MaxMembers { get; set; } = 256;
        public bool OnlyAdminsCanSendMessages { get; set; } = false;
        public bool OnlyAdminsCanAddMembers { get; set; } = false;
        public bool OnlyAdminsCanEditGroupInfo { get; set; } = true;

        // Navigation properties
        public virtual ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }

}
