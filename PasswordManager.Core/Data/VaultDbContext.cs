using Microsoft.EntityFrameworkCore;
using PasswordManager.Core.Entities;

namespace PasswordManager.Core.Data
{
    /// <summary>
    /// EF Core context for Supabase (PostgreSQL). Replaces Cosmos DB: UseNpgsql instead of UseCosmos,
    /// ToTable instead of ToContainer, no partition keys; use indexes and foreign keys instead.
    /// Schema changes: use EF Core migrations (dotnet ef migrations add ... / dotnet ef database update);
    /// Cosmos did not support migrations (EnsureCreated only).
    /// </summary>
    public class VaultDbContext : DbContext
    {
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<VaultEntryEntity> VaultEntries { get; set; }

        public VaultDbContext(DbContextOptions<VaultDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- Users table (relational). Cosmos had ToContainer("Users") + HasPartitionKey(e => e.UserId). ----
            modelBuilder.Entity<UserEntity>(entity =>
            {
                entity.ToTable("Users"); // PostgreSQL table name (was Cosmos container name)
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Email)
                    .IsRequired();
                // Unique index: database guarantees no duplicate emails (Cosmos could not enforce this).
                entity.HasIndex(e => e.Email)
                    .IsUnique();

                entity.Property(e => e.Salt)
                    .IsRequired();

                entity.Property(e => e.EncryptedVerificationToken)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .IsRequired();
            });

            // ---- VaultEntries table. UserId is now a real foreign key (Cosmos had no FK support). ----
            modelBuilder.Entity<VaultEntryEntity>(entity =>
            {
                entity.ToTable("VaultEntries");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.EncryptedData)
                    .IsRequired();

                entity.Property(e => e.UserId)
                    .IsRequired();
                // Index for fast "get all entries for this user" queries (replaces Cosmos partition key usage).
                entity.HasIndex(e => e.UserId);

                // Relationship: each entry belongs to one user; one user has many entries.
                // Database enforces that every VaultEntry.UserId references an existing Users.Id.
                entity.HasOne<UserEntity>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade); // When a user is deleted, their vault entries are deleted too.

                entity.Property(e => e.CreatedAt)
                    .IsRequired();

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
            });
        }
    }
}
