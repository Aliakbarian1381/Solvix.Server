namespace Solvix.Server.Core.Entities
{
    public class UserConnection
    {
        public string ConnectionId { get; set; } = null!;
        public long UserId { get; set; }
        public AppUser User { get; set; } = null!;
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    }
}
