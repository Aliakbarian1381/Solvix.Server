namespace Solvix.Server.Application.DTOs
{
    public class GroupSettingsDto
    {
        public int MaxMembers { get; set; } = 256;
        public bool OnlyAdminsCanSendMessages { get; set; } = false;
        public bool OnlyAdminsCanAddMembers { get; set; } = false;
        public bool OnlyAdminsCanEditInfo { get; set; } = true;
        public bool OnlyAdminsCanDeleteMessages { get; set; } = true;
        public bool AllowMemberToLeave { get; set; } = true;
        public bool IsPublic { get; set; } = false;
        public string? JoinLink { get; set; }

        public bool OnlyAdminsCanEditGroupInfo => OnlyAdminsCanEditInfo;
    }
}
