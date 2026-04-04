using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;

namespace TripPlanner.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripParticipant> TripParticipants { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TripParticipant>()
            .HasKey(tp => new { tp.TripId, tp.UserId });

        modelBuilder.Entity<TripParticipant>()
            .HasOne(tp => tp.Trip)
            .WithMany(t => t.Participants)
            .HasForeignKey(tp => tp.TripId);

        modelBuilder.Entity<TripParticipant>()
            .HasOne(tp => tp.User)
            .WithMany(u => u.TripParticipants)
            .HasForeignKey(tp => tp.UserId);

        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Owner)
            .WithMany(u => u.OwnedTrips)
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId);
    }
}