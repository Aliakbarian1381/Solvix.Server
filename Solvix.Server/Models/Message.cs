namespace Solvix.Server.Models
{
    public class Message
    {
        public int Id { get; set; }
        public long SenderId { get; set; }
        public AppUser Sender { get; set; }
        public long RecipientId { get; set; }
        public AppUser Recipient { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
