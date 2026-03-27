using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minit.Api.Data;
using Minit.Api.Domain;
using Minit.Api.Dtos;
using Minit.Api.Security;
using Minit.Api.Services;

namespace Minit.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin")
            .WithTags("Admin")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();

        group.MapPatch("/users/{userId:guid}/limit", SetUserLimitAsync)
            .WithName("SetUserLimit")
            .WithSummary("Set user monthly limit in seconds or minutes.")
            .Produces<UserLimitResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/usage-adjustments", CreateUsageAdjustmentAsync)
            .WithName("CreateUsageAdjustment")
            .WithSummary("Create a monthly usage adjustment and apply it.")
            .Produces<UsageAdjustmentResponseDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> SetUserLimitAsync(
        [FromRoute] Guid userId,
        [FromBody] AdminSetUserLimitRequestDto request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Minutes is null && request.Seconds is null)
        {
            return Results.Problem(
                title: "Validation failed",
                detail: "Provide either minutes or seconds.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "validation_error" });
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Results.Problem(
                title: "User not found",
                detail: "User does not exist.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "user_not_found" });
        }

        var newSeconds = request.Seconds ?? request.Minutes!.Value * 60;
        if (newSeconds <= 0)
        {
            return Results.Problem(
                title: "Validation failed",
                detail: "Monthly limit must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "validation_error" });
        }

        user.MonthlyLimitSeconds = newSeconds;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UserLimitResponseDto(
            user.Id,
            user.MonthlyLimitSeconds,
            user.MonthlyLimitSeconds / 60));
    }

    private static async Task<IResult> CreateUsageAdjustmentAsync(
        [FromBody] CreateUsageAdjustmentRequestDto request,
        AppDbContext dbContext,
        IQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        if (!IsValidMonth(request.MonthYYYYMM))
        {
            return Results.Problem(
                title: "Invalid month",
                detail: "MonthYYYYMM is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_month" });
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Results.Problem(
                title: "User not found",
                detail: "User does not exist.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["errorCode"] = "user_not_found" });
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var usage = await quotaService.GetOrCreateMonthlyUsageAsync(request.UserId, request.MonthYYYYMM, cancellationToken);
        usage.UsedSeconds = Math.Max(0, usage.UsedSeconds + request.DeltaSeconds);
        usage.UpdatedAt = DateTimeProvider.UtcNow;

        var adjustment = new UsageAdjustment
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            MonthYYYYMM = request.MonthYYYYMM,
            DeltaSeconds = request.DeltaSeconds,
            Reason = request.Reason.Trim(),
            CreatedAt = DateTimeProvider.UtcNow
        };

        dbContext.UsageAdjustments.Add(adjustment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Results.Created(
            $"/api/admin/usage-adjustments/{adjustment.Id}",
            new UsageAdjustmentResponseDto(
                adjustment.Id,
                adjustment.UserId,
                adjustment.MonthYYYYMM,
                adjustment.DeltaSeconds,
                adjustment.Reason,
                adjustment.CreatedAt,
                usage.UsedSeconds));
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
