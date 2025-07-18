namespace Solvix.Server.Core.Entities
{
    public class Chat
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; } // تغییر از GroupImageUrl
        public bool IsGroup { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastMessageTime { get; set; }
        public string? LastMessage { get; set; }
        public int UnreadCount { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<User> Participants { get; set; } = new List<User>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public virtual GroupSettings? GroupSettings { get; set; }
    }

}
