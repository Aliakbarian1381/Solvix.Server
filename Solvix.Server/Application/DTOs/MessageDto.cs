namespace Solvix.Server.Application.DTOs
{
    public class MessageDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public long SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public Guid ChatId { get; set; }

        // Changed to support multiple read statuses
        public List<MessageReadStatusDto> ReadStatuses { get; set; } = new List<MessageReadStatusDto>();

        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }

        // Helper methods for compatibility
        public bool IsReadByUser(long userId)
        {
            return ReadStatuses.Any(rs => rs.ReaderId == userId);
        }

        public DateTime? GetReadTimeByUser(long userId)
        {
            return ReadStatuses.FirstOrDefault(rs => rs.ReaderId == userId)?.ReadAt;
        }
    }

    public class MessageReadStatusDto
    {
        public long ReaderId { get; set; }
        public string ReaderName { get; set; } = "";
        public DateTime ReadAt { get; set; }
    }
}