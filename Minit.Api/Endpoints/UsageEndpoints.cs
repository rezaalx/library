using Microsoft.EntityFrameworkCore;
using Minit.Api.Data;
using Minit.Api.Dtos;
using Minit.Api.Services;

namespace Minit.Api.Endpoints;

public static class UsageEndpoints
{
    public static RouteGroupBuilder MapUsageEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/usage").WithTags("Usage");

        group.MapGet("/{userId:guid}/month/{yyyymm:int}", GetUsageByMonthAsync)
            .WithName("GetUsageByMonth")
            .WithSummary("Get monthly usage for a user")
            .Produces<MonthlyUsageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetUsageByMonthAsync(
        Guid userId,
        int yyyymm,
        AppDbContext dbContext,
        IQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        if (!IsValidMonth(yyyymm))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid month",
                detail: "MonthYYYYMM is invalid.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_month" });
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        if (user is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: "User does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "user_not_found" });
        }

        var usage = await quotaService.GetOrCreateMonthlyUsageAsync(userId, yyyymm, cancellationToken);
        var remaining = Math.Max(0, user.MonthlyLimitSeconds - usage.UsedSeconds);

        return Results.Ok(new MonthlyUsageResponse(
            user.Id,
            yyyymm,
            user.MonthlyLimitSeconds,
            usage.UsedSeconds,
            remaining,
            usage.UpdatedAt));
    }

    private static bool IsValidMonth(int yyyymm)
    {
        if (yyyymm < 190001 || yyyymm > 999912)
        {
            return false;
        }

        var month = yyyymm % 100;
        return month is >= 1 and <= 12;
    }
}
