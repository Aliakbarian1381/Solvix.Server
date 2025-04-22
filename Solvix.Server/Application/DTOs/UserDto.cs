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
        public string? FullName => string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName) ?
            null : $"{FirstName} {LastName}".Trim();
        public string? PhoneNumber { get; set; }
        public string? Token { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActive { get; set; }
    }
}
