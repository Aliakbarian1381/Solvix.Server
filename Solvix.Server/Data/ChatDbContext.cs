// ChatDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Data
{
    public class ChatDbContext : IdentityDbContext<AppUser, IdentityRole<long>, long>
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
        {
        }

        public DbSet<Message> Messages { get; set; }
        public DbSet<UserConnection> UserConnections { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<ChatParticipant> ChatParticipants { get; set; }
        public DbSet<UserContact> UserContacts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);



            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.HasOne(m => m.Sender)
                      .WithMany(u => u.SentMessages)
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Chat)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ChatId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserConnection>(entity =>
            {
                entity.HasKey(e => e.ConnectionId);
                entity.HasOne(d => d.User)
                      .WithMany(p => p.Connections)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .HasConstraintName("FK_UserConnections_User");
            });

            modelBuilder.Entity<Chat>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<ChatParticipant>(entity =>
            {
                entity.HasKey(cp => new { cp.ChatId, cp.UserId });

                entity.HasOne(cp => cp.Chat)
                      .WithMany(c => c.Participants)
                      .HasForeignKey(cp => cp.ChatId);

                entity.HasOne(cp => cp.User)
                      .WithMany()
                      .HasForeignKey(cp => cp.UserId);
            });

            modelBuilder.Entity<UserContact>()
            .HasKey(uc => new { uc.OwnerUserId, uc.ContactUserId });

            modelBuilder.Entity<UserContact>()
                .HasOne(uc => uc.OwnerUser)
                .WithMany()
                .HasForeignKey(uc => uc.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserContact>()
                .HasOne(uc => uc.ContactUser)
                .WithMany()
                .HasForeignKey(uc => uc.ContactUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
