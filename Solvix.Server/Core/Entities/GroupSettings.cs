using System.ComponentModel.DataAnnotations.Schema;

namespace Solvix.Server.Core.Entities
{
    public class GroupSettings
    {
        public long Id { get; set; }
        public Guid ChatId { get; set; }
        public int MaxMembers { get; set; } = 256;
        public bool OnlyAdminsCanSendMessages { get; set; } = false;
        public bool OnlyAdminsCanAddMembers { get; set; } = false;
        public bool OnlyAdminsCanEditInfo { get; set; } = true;
        public bool OnlyAdminsCanDeleteMessages { get; set; } = true;
        public bool AllowMemberToLeave { get; set; } = true;
        public bool IsPublic { get; set; } = false;
        public string? JoinLink { get; set; }

        [ForeignKey(nameof(ChatId))]
        public virtual Chat Chat { get; set; } = null!;
    }
}
