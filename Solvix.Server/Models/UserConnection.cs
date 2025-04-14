namespace Solvix.Server.Models
{
    public class UserConnection
    {
        public string ConnectionId { get; set; }
        public long UserId { get; set; }
        public AppUser User { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    }
}
