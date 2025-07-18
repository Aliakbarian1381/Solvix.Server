using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Solvix.Server.Core.Entities
{
    public class GroupSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid ChatId { get; set; }

        public int MaxMembers { get; set; } = 256;
        public bool OnlyAdminsCanSendMessages { get; set; } = false;
        public bool OnlyAdminsCanAddMembers { get; set; } = false;
        public bool OnlyAdminsCanEditInfo { get; set; } = true;
        public bool OnlyAdminsCanDeleteMessages { get; set; } = true;
        public bool AllowMemberToLeave { get; set; } = true;
        public bool IsPublic { get; set; } = false;
        public string? JoinLink { get; set; }

        // Navigation Property
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;
    }
}
