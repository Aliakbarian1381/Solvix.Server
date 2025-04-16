namespace Solvix.Server.Models
{
    public class ChatParticipant
    {
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; } = default!;

        public long UserId { get; set; }
        public AppUser User { get; set; } = default!;
    }
}
