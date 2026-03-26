using Microsoft.EntityFrameworkCore;
using Workspace.Data;
using Workspace.Dtos.Usage;

namespace Workspace.Endpoints;

public static class UsageEndpoints
{
    public static RouteGroupBuilder MapUsageEndpoints(this RouteGroupBuilder group)
    {
        var usage = group.MapGroup("/usage").WithTags("Usage");

        usage.MapGet("/{userId:guid}/month/{yyyymm:int}", GetMonthlyUsage)
            .WithName("GetMonthlyUsage")
            .Produces<UsageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return usage;
    }

    public static bool IsValidMonth(int month)
    {
        var year = month / 100;
        var m = month % 100;
        return year is >= 2000 and <= 2999 && m is >= 1 and <= 12;
    }

    private static async Task<IResult> GetMonthlyUsage(
        Guid userId,
        int yyyymm,
        AppDbContext dbContext,
        CancellationToken ct)
    {
        if (!IsValidMonth(yyyymm))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["yyyymm"] = ["Month must be in YYYYMM format with month 01-12."]
            });
        }

        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return Results.Problem(
                title: "User not found",
                detail: "No user exists for the provided userId.",
                statusCode: StatusCodes.Status404NotFound,
                type: "not_found");
        }

        var usage = await dbContext.MonthlyUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MonthYYYYMM == yyyymm, ct);

        var usedSeconds = usage?.UsedSeconds ?? 0;
        var remainingSeconds = user.MonthlyLimitSeconds - usedSeconds;
        var updatedAt = usage?.UpdatedAt ?? DateTime.UtcNow;

        return Results.Ok(new UsageResponse(
            userId,
            yyyymm,
            usedSeconds,
            remainingSeconds,
            user.MonthlyLimitSeconds,
            updatedAt));
    }
}
