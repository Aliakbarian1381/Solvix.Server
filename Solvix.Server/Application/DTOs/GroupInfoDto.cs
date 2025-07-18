using Solvix.Server.Core.Entities;

namespace Solvix.Server.Application.DTOs
{
    public class GroupInfoDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public string? GroupImageUrl { get; set; }
        public long OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int MembersCount { get; set; }
        public GroupSettingsDto Settings { get; set; } = new();
        public List<GroupMemberDto> Members { get; set; } = new();
    }

    

   

    public class UpdateGroupDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? GroupImageUrl { get; set; }
    }

    

    public class UpdateMemberRoleDto
    {
        public GroupRole NewRole { get; set; }
    }
}
