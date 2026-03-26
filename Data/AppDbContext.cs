using Microsoft.EntityFrameworkCore;
using Workspace.Domain;

namespace Workspace.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();
    public DbSet<MonthlyUsage> MonthlyUsages => Set<MonthlyUsage>();
    public DbSet<UsageAdjustment> UsageAdjustments => Set<UsageAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("users");
        user.HasKey(x => x.Id);
        user.Property(x => x.DisplayName).IsRequired().HasMaxLength(80);
        user.Property(x => x.Code).IsRequired().HasMaxLength(8).IsFixedLength();
        user.Property(x => x.MonthlyLimitSeconds).HasDefaultValue(6000);
        user.Property(x => x.IsActive).HasDefaultValue(true);
        user.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        user.HasIndex(x => x.Code).IsUnique();

        var contact = modelBuilder.Entity<Contact>();
        contact.ToTable("contacts");
        contact.HasKey(x => x.Id);
        contact.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        contact.HasOne(x => x.OwnerUser)
            .WithMany(x => x.OwnedContacts)
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        contact.HasOne(x => x.ContactUser)
            .WithMany(x => x.ContactOfUsers)
            .HasForeignKey(x => x.ContactUserId)
            .OnDelete(DeleteBehavior.Restrict);
        contact.HasIndex(x => new { x.OwnerUserId, x.ContactUserId }).IsUnique();

        var callSession = modelBuilder.Entity<CallSession>();
        callSession.ToTable("call_sessions");
        callSession.HasKey(x => x.Id);
        callSession.Property(x => x.Provider).IsRequired().HasMaxLength(40);
        callSession.Property(x => x.ProviderRoomId).HasMaxLength(200);
        callSession.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        callSession.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        callSession.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        callSession.Property(x => x.EndedAt).HasColumnType("timestamp with time zone");
        callSession.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedCallSessions)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        var callParticipant = modelBuilder.Entity<CallParticipant>();
        callParticipant.ToTable("call_participants");
        callParticipant.HasKey(x => x.Id);
        callParticipant.Property(x => x.JoinedAt).HasColumnType("timestamp with time zone");
        callParticipant.Property(x => x.LeftAt).HasColumnType("timestamp with time zone");
        callParticipant.Property(x => x.BilledSeconds).HasDefaultValue(0);
        callParticipant.HasOne(x => x.CallSession)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.CallSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        callParticipant.HasOne(x => x.User)
            .WithMany(x => x.CallParticipants)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        callParticipant.HasIndex(x => new { x.CallSessionId, x.UserId }).IsUnique();

        var monthlyUsage = modelBuilder.Entity<MonthlyUsage>();
        monthlyUsage.ToTable("monthly_usage");
        monthlyUsage.HasKey(x => new { x.UserId, x.MonthYYYYMM });
        monthlyUsage.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        monthlyUsage.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var usageAdjustment = modelBuilder.Entity<UsageAdjustment>();
        usageAdjustment.ToTable("usage_adjustments");
        usageAdjustment.HasKey(x => x.Id);
        usageAdjustment.Property(x => x.Reason).IsRequired().HasMaxLength(200);
        usageAdjustment.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        usageAdjustment.HasOne(x => x.User)
            .WithMany(x => x.UsageAdjustments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
