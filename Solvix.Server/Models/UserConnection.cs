namespace Solvix.Server.Models
{
    public class UserConnection
    {
        public string ConnectionId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    }
}
