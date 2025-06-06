namespace Solvix.Server.Application.DTOs
{
    public class SearchResultDto
    {
        public string Id { get; set; } // Can be Guid for chat or long for user
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? AvatarText { get; set; }
        public string Type { get; set; } // "chat" or "user"
        public object Entity { get; set; } = default!; // The full ChatDto or UserDto
    }
}