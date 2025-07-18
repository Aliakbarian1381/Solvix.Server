using Solvix.Server.Core.Entities;

namespace Solvix.Server.Application.DTOs
{
    public class GroupInfoDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public string? GroupImageUrl { get; set; } // اضافه شده
        public long OwnerId { get; set; }
        public string OwnerName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int MembersCount { get; set; }
        public List<GroupMemberDto> Members { get; set; } = new List<GroupMemberDto>();
        public GroupSettingsDto Settings { get; set; } = new GroupSettingsDto();
    }
}
