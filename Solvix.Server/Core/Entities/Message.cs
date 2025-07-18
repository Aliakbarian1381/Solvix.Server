namespace Solvix.Server.Core.Entities
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
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; } // اضافه شده
        public virtual ICollection<MessageReadStatus> ReadStatuses { get; set; } = new List<MessageReadStatus>();
    }
}
