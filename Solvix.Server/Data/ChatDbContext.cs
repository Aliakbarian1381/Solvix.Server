// Solvix.Server/Data/ChatDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
        public DbSet<GroupSettings> GroupSettings { get; set; }
        public DbSet<UserConnection> UserConnections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Identity Tables Configuration
            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");

                // A user can send many messages
                entity.HasMany(e => e.SentMessages)
                      .WithOne(e => e.Sender)
                      .HasForeignKey(e => e.SenderId)
                      .OnDelete(DeleteBehavior.Restrict); // Avoids cascade delete issues on user deletion

                // A user has many contacts
                entity.HasMany(u => u.Contacts)
                      .WithOne(uc => uc.OwnerUser)
                      .HasForeignKey(uc => uc.OwnerUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // A user can be a contact for many other users
                entity.HasMany(u => u.ContactOf)
                      .WithOne(uc => uc.ContactUser)
                      .HasForeignKey(uc => uc.ContactUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AppRole>(entity =>
            {
                entity.ToTable("Roles");
            });

            // Chat Configuration
            modelBuilder.Entity<Chat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // A chat has many participants (the central relationship)
                entity.HasMany(e => e.Participants)
                      .WithOne(e => e.Chat)
                      .HasForeignKey(e => e.ChatId)
                      .OnDelete(DeleteBehavior.Cascade); // If chat is deleted, participants are removed

                // A chat has many messages
                entity.HasMany(e => e.Messages)
                      .WithOne(e => e.Chat)
                      .HasForeignKey(e => e.ChatId)
                      .OnDelete(DeleteBehavior.Cascade); // If chat is deleted, messages are removed

                // A group chat has one settings configuration
                entity.HasOne(e => e.GroupSettings)
                      .WithOne(e => e.Chat)
                      .HasForeignKey<GroupSettings>(e => e.ChatId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Participant Configuration (The Join Table)
            modelBuilder.Entity<Participant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ChatId, e.UserId }).IsUnique(); // A user can only join a chat once

                // A participant belongs to one user
                entity.HasOne(p => p.User)
                      .WithMany(u => u.Chats)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // If user is deleted, their participations are removed

                entity.Property(e => e.Role).HasConversion<string>(); // Store enum as string
            });

            // Message Configuration
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SentAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Content).IsRequired().HasMaxLength(5000);
                entity.Property(e => e.IsEdited).HasDefaultValue(false);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            });

            // MessageReadStatus Configuration
            modelBuilder.Entity<MessageReadStatus>(entity =>
            {
                // Composite key to ensure a user can only mark a message as read once
                entity.HasKey(e => new { e.MessageId, e.ReaderId });

                entity.HasOne(e => e.Message)
                      .WithMany(e => e.ReadStatuses)
                      .HasForeignKey(e => e.MessageId)
                      .OnDelete(DeleteBehavior.Cascade); // If message is deleted, read statuses are removed

                entity.HasOne(e => e.Reader)
                      .WithMany() // No inverse navigation property needed on AppUser for this
                      .HasForeignKey(e => e.ReaderId)
                      .OnDelete(DeleteBehavior.Restrict); // Don't delete message if reader is deleted
            });

            // UserContact Configuration
            modelBuilder.Entity<UserContact>(entity =>
            {
                entity.HasKey(e => new { e.OwnerUserId, e.ContactUserId });
            });

            // GroupSettings Configuration
            modelBuilder.Entity<GroupSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // UserConnection Configuration
            modelBuilder.Entity<UserConnection>(entity =>
            {
                entity.HasKey(e => e.ConnectionId);
                entity.HasIndex(e => e.UserId);

                entity.HasOne(e => e.User)
                      .WithMany(e => e.Connections)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}