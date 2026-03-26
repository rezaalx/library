using Microsoft.EntityFrameworkCore;
using Workspace.Data;
using Workspace.Domain;

namespace Workspace.Services;

public sealed class QuotaService(AppDbContext dbContext)
{
    public static int GetMonthUtc(DateTime utcNow) => utcNow.Year * 100 + utcNow.Month;

    public async Task<MonthlyUsage> GetOrCreateMonthlyUsage(Guid userId, int monthYYYYMM, CancellationToken ct = default)
    {
        var usage = await dbContext.MonthlyUsages
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MonthYYYYMM == monthYYYYMM, ct);

        if (usage is not null)
        {
            return usage;
        }

        var nowUtc = DateTime.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO monthly_usage ("UserId", "MonthYYYYMM", "UsedSeconds", "UpdatedAt")
            VALUES ({userId}, {monthYYYYMM}, {0}, {nowUtc})
            ON CONFLICT ("UserId", "MonthYYYYMM") DO NOTHING
            """,
            ct);

        return await dbContext.MonthlyUsages
            .FirstAsync(x => x.UserId == userId && x.MonthYYYYMM == monthYYYYMM, ct);
    }

    public async Task<int> CheckRemainingSeconds(Guid userId, DateTime utcNow, CancellationToken ct = default)
    {
        var month = GetMonthUtc(utcNow);

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return 0;
        }

        var usage = await dbContext.MonthlyUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MonthYYYYMM == month, ct);

        var used = usage?.UsedSeconds ?? 0;
        return user.MonthlyLimitSeconds - used;
    }

    public async Task ApplyBilling(Guid billedUserId, int monthYYYYMM, int billedSeconds, CancellationToken ct = default)
    {
        billedSeconds = Math.Max(0, billedSeconds);
        var usage = await GetOrCreateMonthlyUsage(billedUserId, monthYYYYMM, ct);
        usage.UsedSeconds += billedSeconds;
        usage.UpdatedAt = DateTime.UtcNow;
    }
}
