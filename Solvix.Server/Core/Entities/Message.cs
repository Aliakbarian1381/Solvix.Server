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

        // Message status properties  
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // Navigation property for read statuses - many-to-many through MessageReadStatus
        public virtual ICollection<MessageReadStatus> ReadStatuses { get; set; } = new List<MessageReadStatus>();

        // Helper methods for read status
        public bool IsReadByUser(long userId)
        {
            return ReadStatuses.Any(rs => rs.ReaderId == userId);
        }

        public DateTime? GetReadTimeByUser(long userId)
        {
            return ReadStatuses.FirstOrDefault(rs => rs.ReaderId == userId)?.ReadAt;
        }

        public List<long> GetReadByUserIds()
        {
            return ReadStatuses.Select(rs => rs.ReaderId).ToList();
        }

        public int GetReadCount()
        {
            return ReadStatuses.Count;
        }
    }
}