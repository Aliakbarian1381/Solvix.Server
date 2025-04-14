using Microsoft.EntityFrameworkCore;
using Solvix.Server.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Solvix.Server.Data


{
    public class ChatDbContext : IdentityDbContext<AppUser, IdentityRole<long>, long>
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
        {
        }

        public DbSet<Message> Messages { get; set; }
        public DbSet<UserConnection> UserConnections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasOne(d => d.Sender)
                    .WithMany(p => p.SentMessages)
                    .HasForeignKey(d => d.SenderId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Messages_Sender");

                entity.HasOne(d => d.Recipient)
                    .WithMany(p => p.ReceivedMessages)
                    .HasForeignKey(d => d.RecipientId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Messages_Recipient");
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

            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");
            });
        }
    }
}


