namespace Solvix.Server.Application.DTOs
{
    public class ChatDto
    {
        public Guid Id { get; set; }
        public bool IsGroup { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? GroupImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public List<UserDto> Participants { get; set; } = new List<UserDto>();
        public long? OwnerId { get; set; }

        // Helper properties
        public bool HasUnreadMessages => UnreadCount > 0;
        public string DisplayTitle => IsGroup ? Title :
            (Participants.FirstOrDefault()?.DisplayName ?? "چت");
    }
}
