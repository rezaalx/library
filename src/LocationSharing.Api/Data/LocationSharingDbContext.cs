using LocationSharing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LocationSharing.Api.Data;

public class LocationSharingDbContext(DbContextOptions<LocationSharingDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripMember> TripMembers => Set<TripMember>();
    public DbSet<MemberLocationLatest> MemberLocationLatests => Set<MemberLocationLatest>();
    public DbSet<MemberLocationHistory> MemberLocationHistories => Set<MemberLocationHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.ImageUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Code).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
        });

        modelBuilder.Entity<TripMember>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.TripId, x.MemberId }).IsUnique();

            entity.HasOne(x => x.Member)
                .WithMany(x => x.TripMembers)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Trip)
                .WithMany(x => x.TripMembers)
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemberLocationLatest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.TripId, x.MemberId }).IsUnique();

            entity.HasOne(x => x.Member)
                .WithMany(x => x.LatestLocations)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Trip)
                .WithMany(x => x.LatestLocations)
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemberLocationHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.TripId, x.MemberId, x.RecordedAt }).IsDescending(false, false, true);

            entity.HasOne(x => x.Member)
                .WithMany(x => x.LocationHistory)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Trip)
                .WithMany(x => x.LocationHistory)
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
