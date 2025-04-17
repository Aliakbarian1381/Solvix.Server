namespace Solvix.Server.Models
{
    public class Message
    {
        public int Id { get; set; }
        public long SenderId { get; set; }
        public AppUser Sender { get; set; } = default!;
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; } = default!;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public bool IsRead { get; set; } = false;

    }
}
