using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class UserContact
    {
        public long OwnerUserId { get; set; }
        public AppUser OwnerUser { get; set; } = null!;

        public long ContactUserId { get; set; }
        public AppUser ContactUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastInteractionAt { get; set; }

        public bool IsFavorite { get; set; } = false;
        public bool IsBlocked { get; set; } = false;
        public string? DisplayName { get; set; }

        public static string GetUniqueKey(long ownerId, long contactId)
        {
            return $"{ownerId}_{contactId}";
        }
    }
}