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

        // اضافه کردن فیلدهای مورد نیاز برای group
        public string? GroupImageUrl { get; set; }

        public string? LastMessage { get; set; }
        public int UnreadCount { get; set; } = 0;

        public DateTime? LastMessageTime { get; set; }

        public int MaxMembers { get; set; } = 256;

        public bool OnlyAdminsCanAddMembers { get; set; } = false;

        public bool OnlyAdminsCanEditGroupInfo { get; set; } = true;

        public bool OnlyAdminsCanSendMessages { get; set; } = false;

        public long? OwnerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties - حذف تکراری ها
        public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public virtual GroupSettings? GroupSettings { get; set; }
    }

}
