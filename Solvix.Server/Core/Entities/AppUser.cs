﻿using Microsoft.AspNetCore.Identity;


namespace Solvix.Server.Core.Entities
{
    public class AppUser : IdentityUser<long>
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastActiveAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public string? FcmToken { get; set; }
        public string? ProfilePictureUrl { get; set; }

        public bool IsOnline { get; set; } = false;

        public string Username => UserName ?? "";

        public virtual ICollection<Message> SentMessages { get; set; } = new HashSet<Message>();
        public virtual ICollection<UserConnection> Connections { get; set; } = new HashSet<UserConnection>();
    }
}
