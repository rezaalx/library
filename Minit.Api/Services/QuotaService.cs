using Microsoft.EntityFrameworkCore;
using Minit.Api.Data;
using Minit.Api.Domain;

namespace Minit.Api.Services;

public sealed class QuotaService(AppDbContext dbContext) : IQuotaService
{
    public async Task<MonthlyUsage> GetOrCreateMonthlyUsageAsync(
        Guid userId,
        int monthYYYYMM,
        CancellationToken cancellationToken = default)
    {
        var usage = await dbContext.MonthlyUsages
            .SingleOrDefaultAsync(x => x.UserId == userId && x.MonthYYYYMM == monthYYYYMM, cancellationToken);
        if (usage is not null)
        {
            return usage;
        }

        usage = new MonthlyUsage
        {
            UserId = userId,
            MonthYYYYMM = monthYYYYMM,
            UsedSeconds = 0,
            UpdatedAt = DateTimeProvider.UtcNow
        };
        dbContext.MonthlyUsages.Add(usage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return usage;
    }

    public async Task<int> CheckRemainingSecondsAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var monthYYYYMM = DateTimeProvider.ToMonthYYYYMM(utcNow);
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null)
        {
            return 0;
        }

        var usage = await dbContext.MonthlyUsages
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.MonthYYYYMM == monthYYYYMM, cancellationToken);

        var used = usage?.UsedSeconds ?? 0;
        var remaining = user.MonthlyLimitSeconds - used;
        return Math.Max(remaining, 0);
    }

    public async Task ApplyBillingAsync(
        Guid userId,
        int billedSeconds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (billedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(billedSeconds), "Billed seconds cannot be negative.");
        }

        var monthYYYYMM = DateTimeProvider.ToMonthYYYYMM(utcNow);
        var usage = await dbContext.MonthlyUsages
            .SingleOrDefaultAsync(x => x.UserId == userId && x.MonthYYYYMM == monthYYYYMM, cancellationToken);

        if (usage is null)
        {
            usage = new MonthlyUsage
            {
                UserId = userId,
                MonthYYYYMM = monthYYYYMM,
                UsedSeconds = billedSeconds,
                UpdatedAt = DateTimeProvider.UtcNow
            };
            dbContext.MonthlyUsages.Add(usage);
            return;
        }

        usage.UsedSeconds += billedSeconds;
        usage.UpdatedAt = DateTimeProvider.UtcNow;
    }
}
