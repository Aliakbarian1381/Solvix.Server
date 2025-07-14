using System.Text.Json.Serialization;

namespace Solvix.Server.Application.DTOs
{
    public class UserDto
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [JsonIgnore]
        public string? PhoneNumber { get; set; }
        public string? Token { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActive { get; set; }
        public bool HasChat { get; set; } = false;
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; } = 0;
        public bool IsFavorite { get; set; } = false;
        public bool IsBlocked { get; set; } = false;
        public string? DisplayName { get; set; }
        public DateTime? ContactCreatedAt { get; set; }
        public DateTime? LastInteractionAt { get; set; }

        public string FullName => !string.IsNullOrEmpty(DisplayName)
       ? DisplayName
       : $"{FirstName} {LastName}".Trim();

        public string InitialName => !string.IsNullOrEmpty(FullName)
            ? FullName
            : Username;
    }
}
