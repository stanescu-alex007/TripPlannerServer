using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;

namespace TripPlanner.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripParticipant> TripParticipants => Set<TripParticipant>();
    public DbSet<TripScheduleItem> TripScheduleItems => Set<TripScheduleItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Phone).HasMaxLength(30);
            entity.Property(u => u.ImageUrl).HasMaxLength(500);
            // Optional: an account that only signs in through an external provider
            // has no password at all.
            entity.Property(u => u.PasswordHash).HasMaxLength(512);
            entity.Property(u => u.TwoFactorSecret).HasMaxLength(128);

            // The application lower-cases before writing, so a plain unique index is
            // enough and — unlike a lower(Email) expression index — stays usable by
            // ordinary equality lookups.
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasQueryFilter(u => u.DeletedAt == null);
        });

        // ── Trip ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Location).HasMaxLength(300).IsRequired();
            entity.Property(t => t.TransportType).HasMaxLength(50).IsRequired();
            entity.Property(t => t.Image).HasMaxLength(500);
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.Budget).HasPrecision(12, 2);

            entity.HasOne(t => t.Owner)
                  .WithMany(u => u.OwnedTrips)
                  .HasForeignKey(t => t.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => t.OwnerId);
            entity.HasIndex(t => new { t.StartDate, t.EndDate });

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Trips_DateRange", "\"EndDate\" >= \"StartDate\""));

            entity.HasQueryFilter(t => t.DeletedAt == null);
        });

        // ── TripParticipant ───────────────────────────────────────────────────
        modelBuilder.Entity<TripParticipant>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Email).HasMaxLength(256).IsRequired();
            entity.Property(p => p.Nickname).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Role).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Status).HasMaxLength(30).IsRequired();

            entity.HasOne(p => p.Trip)
                  .WithMany(t => t.Participants)
                  .HasForeignKey(p => p.TripId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.User)
                  .WithMany(u => u.TripParticipants)
                  .HasForeignKey(p => p.UserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => new { p.TripId, p.Email }).IsUnique();
            entity.HasIndex(p => p.UserId);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TripParticipants_Role",
                    "\"Role\" IN ('Owner','Admin','VIP','Participant')");
                t.HasCheckConstraint("CK_TripParticipants_Status",
                    "\"Status\" IN ('Confirmed','NotConfirmed','Declined')");
            });

            entity.HasQueryFilter(p => p.DeletedAt == null);
        });

        // ── TripScheduleItem ──────────────────────────────────────────────────
        modelBuilder.Entity<TripScheduleItem>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Title).HasMaxLength(200).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(1000);
            entity.Property(s => s.Status).HasMaxLength(30).IsRequired()
                  .HasDefaultValue(TripScheduleItemStatus.Pending);

            entity.HasOne(s => s.Trip)
                  .WithMany(t => t.ScheduleItems)
                  .HasForeignKey(s => s.TripId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Matches the read path: filter by trip, ordered by day then position.
            entity.HasIndex(s => new { s.TripId, s.Date, s.Order });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TripScheduleItems_Status",
                    "\"Status\" IN ('Pending','InProgress','Done','Canceled')");
                t.HasCheckConstraint("CK_TripScheduleItems_TimeRange",
                    "\"StartTime\" IS NULL OR \"EndTime\" IS NULL OR \"EndTime\" >= \"StartTime\"");
            });

            entity.HasQueryFilter(s => s.DeletedAt == null);
        });

        // ── RefreshToken ──────────────────────────────────────────────────────
        // Never soft-deleted: a revoked row must remain readable so that replaying it
        // can still be detected, and expired rows are pruned for real.
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(rt => rt.RevokedReason).HasMaxLength(50);
            entity.Property(rt => rt.CreatedByIp).HasMaxLength(64);
            entity.Property(rt => rt.UserAgent).HasMaxLength(512);

            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rt => rt.ReplacedByToken)
                  .WithMany()
                  .HasForeignKey(rt => rt.ReplacedByTokenId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Every refresh is a lookup by hash — must be a unique index, not a scan.
            entity.HasIndex(rt => rt.TokenHash).IsUnique();

            // Detonating a family updates every row sharing the FamilyId.
            entity.HasIndex(rt => rt.FamilyId);
            entity.HasIndex(rt => rt.UserId);

            // Used by the cleanup job.
            entity.HasIndex(rt => rt.ExpiresAt);

            // Mirrors the User filter: once an account is soft-deleted its sessions stop
            // resolving at the data layer, rather than depending on the delete path also
            // remembering to revoke them.
            entity.HasQueryFilter(rt => rt.User.DeletedAt == null);
        });

        // ── UserToken ─────────────────────────────────────────────────────────
        modelBuilder.Entity<UserToken>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(t => t.Purpose).HasMaxLength(50).IsRequired();
            entity.Property(t => t.NewEmail).HasMaxLength(256);

            entity.HasOne(t => t.User)
                  .WithMany(u => u.UserTokens)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => new { t.UserId, t.Purpose });
            entity.HasIndex(t => t.ExpiresAt);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_UserTokens_Purpose",
                "\"Purpose\" IN ('email-verification','password-reset','email-change','two-factor-challenge')"));

            entity.HasQueryFilter(t => t.User.DeletedAt == null);
        });

        // ── UserRecoveryCode ──────────────────────────────────────────────────
        modelBuilder.Entity<UserRecoveryCode>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.CodeHash).HasMaxLength(512).IsRequired();

            entity.HasOne(c => c.User)
                  .WithMany(u => u.RecoveryCodes)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.UserId);
            entity.HasQueryFilter(c => c.User.DeletedAt == null);
        });

        // ── ExternalLogin ─────────────────────────────────────────────────────
        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Provider).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProviderKey).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ProviderEmail).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.PictureUrl).HasMaxLength(500);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.ExternalLogins)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // One identity at a provider maps to exactly one account.
            entity.HasIndex(e => new { e.Provider, e.ProviderKey }).IsUnique();

            // And an account links a given provider at most once.
            entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();

            entity.HasQueryFilter(e => e.User.DeletedAt == null);
        });
    }
}
