﻿namespace Solvix.Server.Application.DTOs
{
    public class GroupMemberDto
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string Role { get; set; } = "Member"; // Owner, Admin, Member
        public DateTime JoinedAt { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastActive { get; set; }
    }
}