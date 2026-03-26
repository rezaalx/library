using Microsoft.EntityFrameworkCore;
using Workspace.Data;
using Workspace.Domain;
using Workspace.Dtos.Admin;
using Workspace.Security;
using Workspace.Services;
using static Workspace.Endpoints.UsageEndpoints;

namespace Workspace.Endpoints;

public static class AdminEndpoints
{
    private static readonly object _monthlyUsageLock = new();

    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin").WithTags("Admin");

        group.MapPatch("/users/{userId:guid}/limit", UpdateLimitAsync)
            .WithName("UpdateUserLimit")
            .Produces<UpdateUserLimitResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/usage-adjustments", CreateUsageAdjustmentAsync)
            .WithName("CreateUsageAdjustment")
            .Produces<CreateUsageAdjustmentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> UpdateLimitAsync(
        Guid userId,
        UpdateUserLimitRequest request,
        HttpRequest httpRequest,
        IConfiguration configuration,
        AppDbContext dbContext,
        CancellationToken ct)
    {
        if (!AdminApiKeyAuth.IsAuthorized(httpRequest, configuration))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                type: "admin_auth_required",
                detail: "A valid X-Admin-Key header is required.");
        }

        if (request.MonthlyLimitSeconds is null && request.MonthlyLimitMinutes is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing limit",
                type: "missing_limit",
                detail: "Provide either monthlyLimitSeconds or monthlyLimitMinutes.");
        }

        if (request.MonthlyLimitMinutes is < 0 || request.MonthlyLimitSeconds is < 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid limit",
                type: "invalid_limit",
                detail: "Monthly limit cannot be negative.");
        }

        var computedSeconds = request.MonthlyLimitSeconds ?? checked(request.MonthlyLimitMinutes!.Value * 60);

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                type: "user_not_found");
        }

        user.MonthlyLimitSeconds = computedSeconds;
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(new UpdateUserLimitResponse(
            user.Id,
            user.MonthlyLimitSeconds,
            user.MonthlyLimitSeconds / 60));
    }

    private static async Task<IResult> CreateUsageAdjustmentAsync(
        UsageAdjustmentRequest request,
        HttpRequest httpRequest,
        IConfiguration configuration,
        AppDbContext dbContext,
        QuotaService quotaService,
        CancellationToken ct)
    {
        if (!AdminApiKeyAuth.IsAuthorized(httpRequest, configuration))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                type: "admin_auth_required",
                detail: "A valid X-Admin-Key header is required.");
        }

        if (!IsValidMonth(request.MonthYYYYMM))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["monthYYYYMM"] = ["Month must be in YYYYMM format with month 01-12."]
            });
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, ct);
        if (user is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                type: "user_not_found");
        }

        if (!UsageEndpoints.IsValidMonth(request.MonthYYYYMM))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["monthYYYYMM"] = ["Month must be in YYYYMM format with month 01-12."]
            });
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            MonthlyUsage usage;
            lock (_monthlyUsageLock)
            {
                // Lightweight process-local lock avoids duplicate inserts before transaction commit.
                usage = quotaService.GetOrCreateMonthlyUsage(request.UserId, request.MonthYYYYMM, ct)
                    .GetAwaiter()
                    .GetResult();
            }
            usage.UsedSeconds += request.DeltaSeconds;
            usage.UpdatedAt = DateTime.UtcNow;

            var adjustment = new UsageAdjustment
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                MonthYYYYMM = request.MonthYYYYMM,
                DeltaSeconds = request.DeltaSeconds,
                Reason = request.Reason.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            dbContext.UsageAdjustments.Add(adjustment);
            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Ok(new CreateUsageAdjustmentResponse(
                adjustment.Id,
                adjustment.UserId,
                adjustment.MonthYYYYMM,
                adjustment.DeltaSeconds,
                adjustment.Reason,
                adjustment.CreatedAt,
                usage.UsedSeconds));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
