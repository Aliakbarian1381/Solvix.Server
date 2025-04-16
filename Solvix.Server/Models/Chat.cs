namespace Solvix.Server.Models
{
    public class Chat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsGroup { get; set; } = false;
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
