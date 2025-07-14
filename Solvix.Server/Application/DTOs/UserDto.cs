using System.Text.Json.Serialization;

namespace Solvix.Server.Application.DTOs
{
    public class UserDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? FcmToken { get; set; }

        // اطلاعات مخاطب
        public bool IsContact { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsBlocked { get; set; }
        public string? DisplayName { get; set; }
        public DateTime? ContactCreatedAt { get; set; }
        public DateTime? LastInteractionAt { get; set; }

        // اطلاعات چت
        public bool HasChat { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }

        // Computed properties
        public string FullName => !string.IsNullOrEmpty(DisplayName) ? DisplayName :
                                 $"{FirstName ?? ""} {LastName ?? ""}".Trim();

        public string DisplayNameOrUsername => !string.IsNullOrEmpty(FullName) ? FullName : UserName;

        public string AvatarInitials
        {
            get
            {
                var name = DisplayNameOrUsername;
                if (string.IsNullOrEmpty(name)) return "?";

                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
                else if (parts.Length == 1)
                    return parts[0][0].ToString().ToUpper();

                return "?";
            }
        }

        public string LastSeenText
        {
            get
            {
                if (IsOnline) return "آنلاین";
                if (!LastActiveAt.HasValue) return "نامشخص";

                var diff = DateTime.UtcNow - LastActiveAt.Value;
                if (diff.TotalMinutes < 1) return "همین الان";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} دقیقه پیش";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ساعت پیش";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} روز پیش";

                return "خیلی وقت پیش";
            }
        }
    }
}
