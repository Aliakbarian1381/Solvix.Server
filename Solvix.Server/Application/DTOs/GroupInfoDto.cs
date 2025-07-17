using Solvix.Server.Core.Entities;

namespace Solvix.Server.Application.DTOs
{
    public class GroupInfoDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? GroupImageUrl { get; set; }
        public long OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int MembersCount { get; set; }
        public GroupSettingsDto Settings { get; set; } = new();
        public List<GroupMemberDto> Members { get; set; } = new();
    }

    public class GroupMemberDto
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public GroupRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
    }

    public class GroupSettingsDto
    {
        public bool OnlyAdminsCanSendMessages { get; set; }
        public bool OnlyAdminsCanAddMembers { get; set; }
        public bool OnlyAdminsCanEditGroupInfo { get; set; }
        public int MaxMembers { get; set; }
    }

    public class UpdateGroupDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? GroupImageUrl { get; set; }
    }

    public class AddMemberDto
    {
        public List<long> UserIds { get; set; } = new();
    }

    public class UpdateMemberRoleDto
    {
        public GroupRole NewRole { get; set; }
    }
}
