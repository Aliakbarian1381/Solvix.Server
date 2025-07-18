namespace Solvix.Server.Core.Entities
{
    public class ChatParticipant
    {
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; } = default!;

        public long UserId { get; set; }
        public AppUser User { get; set; } = default!;

        public GroupRole Role { get; set; } = GroupRole.Member;  // جدید
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;  // جدید
        public bool IsActive { get; set; } = true;  // جدید
        public DateTime? LeftAt { get; set; }  // جدید
    }
}
