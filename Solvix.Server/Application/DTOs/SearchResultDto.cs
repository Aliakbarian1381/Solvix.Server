namespace Solvix.Server.Application.DTOs
{
    public class SearchResultDto
    {
        public required string Id { get; set; }
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public required string Type { get; set; } // "user", "chat", "group"
        public object? Entity { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsOnline { get; set; }
    }
}