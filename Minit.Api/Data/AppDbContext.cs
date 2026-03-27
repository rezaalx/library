using Microsoft.EntityFrameworkCore;
using Minit.Api.Domain;
using Minit.Api.Data.EntityConfigurations;

namespace Minit.Api.Data;

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
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ContactConfiguration());
        modelBuilder.ApplyConfiguration(new CallSessionConfiguration());
        modelBuilder.ApplyConfiguration(new CallParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new MonthlyUsageConfiguration());
        modelBuilder.ApplyConfiguration(new UsageAdjustmentConfiguration());
    }
}
