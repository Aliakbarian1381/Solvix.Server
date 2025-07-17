namespace Solvix.Server.Core.Entities
{
    public class Chat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsGroup { get; set; } = false;
        public string? Title { get; set; }
        public string? Description { get; set; }  // جدید
        public string? GroupImageUrl { get; set; }  // جدید
        public long? OwnerId { get; set; }  // جدید - مالک گروه
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; } = 0;

        // تنظیمات گروه
        public bool OnlyAdminsCanSendMessages { get; set; } = false;
        public bool OnlyAdminsCanAddMembers { get; set; } = false;
        public bool OnlyAdminsCanEditGroupInfo { get; set; } = true;
        public int MaxMembers { get; set; } = 256;

        // Navigation Properties
        public AppUser? Owner { get; set; }
        public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
