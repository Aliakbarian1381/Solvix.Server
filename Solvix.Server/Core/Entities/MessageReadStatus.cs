namespace Solvix.Server.Core.Entities
{
    public class MessageReadStatus
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public long ReaderId { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual Message Message { get; set; } = null!;
        public virtual AppUser Reader { get; set; } = null!;
    }
}