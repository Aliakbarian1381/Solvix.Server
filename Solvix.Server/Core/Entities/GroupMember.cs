using System.ComponentModel.DataAnnotations.Schema;


namespace Solvix.Server.Core.Entities
{
    public class GroupMember
    {
        public long Id { get; set; }
        public Guid ChatId { get; set; }
        public long UserId { get; set; }
        public GroupRole Role { get; set; }
        public DateTime JoinedAt { get; set; }

        [ForeignKey(nameof(ChatId))]
        public virtual Chat Chat { get; set; } = null!;

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}
