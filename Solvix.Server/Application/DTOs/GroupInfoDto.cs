namespace Solvix.Server.Application.DTOs
{
    public class GroupInfoDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? GroupImageUrl { get; set; }
        public long OwnerId { get; set; }
        public string OwnerName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int MembersCount { get; set; }
        public List<GroupMemberDto> Members { get; set; } = new List<GroupMemberDto>();
        public GroupSettingsDto Settings { get; set; } = new GroupSettingsDto();
    }
}