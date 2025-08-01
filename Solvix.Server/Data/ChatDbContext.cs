﻿using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Data
{
    public class ChatDbContext : IdentityDbContext<AppUser, AppRole, long>
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
        {
        }

        public DbSet<Chat> Chats { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageReadStatus> MessageReadStatuses { get; set; }
        public DbSet<UserContact> UserContacts { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupSettings> GroupSettings { get; set; }
        public DbSet<UserConnection> UserConnections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Chat Configuration
            modelBuilder.Entity<Chat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // Configure relationships
                entity.HasMany(e => e.Participants)
                    .WithOne(e => e.Chat)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Messages)
                    .WithOne(e => e.Chat)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.GroupMembers)
                    .WithOne(e => e.Chat)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.GroupSettings)
                    .WithOne(e => e.Chat)
                    .HasForeignKey<GroupSettings>(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Message Configuration - ✅ اصلاح شد
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SentAt).HasDefaultValueSql("GETUTCDATE()");

                // ✅ تنها relationship درست با Sender
                entity.HasOne(e => e.Sender)
                    .WithMany(e => e.SentMessages)
                    .HasForeignKey(e => e.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Chat)
                    .WithMany(e => e.Messages)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure other properties
                entity.Property(e => e.Content).IsRequired().HasMaxLength(5000);
                entity.Property(e => e.IsEdited).HasDefaultValue(false);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            });

            // MessageReadStatus Configuration
            modelBuilder.Entity<MessageReadStatus>(entity =>
            {
                entity.HasKey(e => new { e.MessageId, e.ReaderId });

                entity.HasOne(e => e.Message)
                    .WithMany(e => e.ReadStatuses)
                    .HasForeignKey(e => e.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Reader)
                    .WithMany()
                    .HasForeignKey(e => e.ReaderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Participant Configuration
            modelBuilder.Entity<Participant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ChatId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Chat)
                    .WithMany(e => e.Participants)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // GroupMember Configuration
            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ChatId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Chat)
                    .WithMany(e => e.GroupMembers)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure enum
                entity.Property(e => e.Role)
                    .HasConversion<string>();
            });

            // GroupSettings Configuration
            modelBuilder.Entity<GroupSettings>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Chat)
                    .WithOne(e => e.GroupSettings)
                    .HasForeignKey<GroupSettings>(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserContact Configuration - ✅ با توجه به ساختار موجود database
            modelBuilder.Entity<UserContact>(entity =>
            {
                // طبق ModelSnapshot فعلی، composite key داریم
                entity.HasKey(e => new { e.OwnerUserId, e.ContactUserId });

                entity.HasOne(e => e.OwnerUser)
                    .WithMany()
                    .HasForeignKey(e => e.OwnerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ContactUser)
                    .WithMany()
                    .HasForeignKey(e => e.ContactUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // UserConnection Configuration
            modelBuilder.Entity<UserConnection>(entity =>
            {
                entity.HasKey(e => e.ConnectionId); // ✅ اصلاح شد - ConnectionId primary key است
                entity.HasIndex(e => e.UserId);

                entity.HasOne(e => e.User)
                    .WithMany(e => e.Connections)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Identity Tables Configuration
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");
                // ✅ بدون relationship اضافی که باعث AppUserId میشد
            });

            modelBuilder.Entity<AppRole>(entity =>
            {
                entity.ToTable("Roles");
            });
        }
    }
}