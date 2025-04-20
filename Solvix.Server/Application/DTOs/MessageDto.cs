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
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
    }
}
