using Microsoft.EntityFrameworkCore;
using Workspace.Data;
using Workspace.Domain;
using Workspace.Dtos.Calls;
using Workspace.Services;

namespace Workspace.Endpoints;

public static class CallEndpoints
{
    public static RouteGroupBuilder MapCallEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/calls").WithTags("Calls");

        group.MapPost("/start", StartCallAsync)
            .WithName("StartCall")
            .WithSummary("Start a call session")
            .Produces<StartCallResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{callId:guid}/join", JoinCallAsync)
            .WithName("JoinCall")
            .WithSummary("Join an active call")
            .Produces<JoinCallResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{callId:guid}/end", EndCallAsync)
            .WithName("EndCall")
            .WithSummary("End an active call and bill creator")
            .Produces<EndCallResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> StartCallAsync(
        StartCallRequest request,
        AppDbContext db,
        QuotaService quotaService,
        CancellationToken ct)
    {
        if (request.CreatedByUserId == Guid.Empty || request.CalleeUserId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userIds"] = ["createdByUserId and calleeUserId are required."]
            });
        }

        if (request.CreatedByUserId == request.CalleeUserId)
        {
            return Results.Problem(
                title: "Invalid call request",
                detail: "Caller and callee cannot be the same user.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_call_participants" });
        }

        var users = await db.Users
            .Where(x => x.Id == request.CreatedByUserId || x.Id == request.CalleeUserId)
            .ToListAsync(ct);

        var creator = users.FirstOrDefault(x => x.Id == request.CreatedByUserId && x.IsActive);
        if (creator is null)
        {
            return Results.Problem(
                title: "Creator not found",
                detail: "The creating user does not exist or is inactive.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "creator_not_found" });
        }

        var callee = users.FirstOrDefault(x => x.Id == request.CalleeUserId && x.IsActive);
        if (callee is null)
        {
            return Results.Problem(
                title: "Callee not found",
                detail: "The callee user does not exist or is inactive.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "callee_not_found" });
        }

        var nowUtc = DateTime.UtcNow;
        var remaining = await quotaService.CheckRemainingSeconds(request.CreatedByUserId, nowUtc, ct);
        if (remaining <= 0)
        {
            return Results.Problem(
                title: "Quota exceeded",
                detail: "No remaining monthly call seconds.",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = "quota_exceeded" });
        }

        var session = new CallSession
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = request.CreatedByUserId,
            Provider = string.IsNullOrWhiteSpace(request.Provider) ? "internal" : request.Provider.Trim(),
            ProviderRoomId = string.IsNullOrWhiteSpace(request.ProviderRoomId) ? null : request.ProviderRoomId.Trim(),
            Status = CallSessionStatus.Active,
            CreatedAt = nowUtc,
            StartedAt = nowUtc
        };

        var creatorParticipant = new CallParticipant
        {
            Id = Guid.NewGuid(),
            CallSessionId = session.Id,
            UserId = request.CreatedByUserId,
            JoinedAt = nowUtc,
            BilledSeconds = 0
        };

        db.CallSessions.Add(session);
        db.CallParticipants.Add(creatorParticipant);
        await db.SaveChangesAsync(ct);

        var response = new StartCallResponse
        {
            CallId = session.Id,
            Status = session.Status,
            StartedAt = session.StartedAt!.Value
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> JoinCallAsync(
        Guid callId,
        JoinCallRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["userId is required."]
            });
        }

        var nowUtc = DateTime.UtcNow;
        var call = await db.CallSessions.FirstOrDefaultAsync(x => x.Id == callId, ct);
        if (call is null)
        {
            return Results.Problem(
                title: "Call not found",
                detail: "Call session does not exist.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "call_not_found" });
        }

        if (call.Status != CallSessionStatus.Active)
        {
            return Results.Problem(
                title: "Call is not active",
                detail: "Only active calls can be joined.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "call_not_active" });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId && x.IsActive, ct);
        if (user is null)
        {
            return Results.Problem(
                title: "User not found",
                detail: "Joining user does not exist or is inactive.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "user_not_found" });
        }

        var existing = await db.CallParticipants
            .FirstOrDefaultAsync(x => x.CallSessionId == callId && x.UserId == request.UserId, ct);
        if (existing is not null)
        {
            return Results.Ok(new JoinCallResponse
            {
                CallId = callId,
                UserId = request.UserId,
                JoinedAt = existing.JoinedAt,
                AlreadyJoined = true
            });
        }

        var participant = new CallParticipant
        {
            Id = Guid.NewGuid(),
            CallSessionId = callId,
            UserId = request.UserId,
            JoinedAt = nowUtc,
            BilledSeconds = 0
        };

        db.CallParticipants.Add(participant);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new JoinCallResponse
        {
            CallId = callId,
            UserId = request.UserId,
            JoinedAt = participant.JoinedAt,
            AlreadyJoined = false
        });
    }

    private static async Task<IResult> EndCallAsync(
        Guid callId,
        EndCallRequest request,
        AppDbContext db,
        QuotaService quotaService,
        CancellationToken ct)
    {
        if (request.EndedByUserId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["endedByUserId"] = ["endedByUserId is required."]
            });
        }

        var nowUtc = DateTime.UtcNow;
        var call = await db.CallSessions.FirstOrDefaultAsync(x => x.Id == callId, ct);
        if (call is null)
        {
            return Results.Problem(
                title: "Call not found",
                detail: "Call session does not exist.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "call_not_found" });
        }

        var endingUserExists = await db.Users.AnyAsync(x => x.Id == request.EndedByUserId, ct);
        if (!endingUserExists)
        {
            return Results.Problem(
                title: "Ending user not found",
                detail: "The user ending the call does not exist.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = "ended_by_user_not_found" });
        }

        if (call.Status is CallSessionStatus.Ended or CallSessionStatus.Cancelled)
        {
            var existingUsage = await db.MonthlyUsages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == call.CreatedByUserId && x.MonthYYYYMM == QuotaService.GetMonthUtc(call.EndedAt ?? nowUtc), ct);
            return Results.Ok(new EndCallResponse
            {
                CallId = call.Id,
                BilledSeconds = 0,
                MonthYYYYMM = QuotaService.GetMonthUtc(call.EndedAt ?? nowUtc),
                UsedSeconds = existingUsage?.UsedSeconds ?? 0
            });
        }

        if (call.StartedAt is null)
        {
            return Results.Problem(
                title: "Invalid call state",
                detail: "Call does not have a start timestamp.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "call_missing_started_at" });
        }

        var callDurationSeconds = (int)Math.Max(0, (nowUtc - call.StartedAt.Value).TotalSeconds);
        var billableSeconds = callDurationSeconds;
        var month = QuotaService.GetMonthUtc(nowUtc);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            call.Status = CallSessionStatus.Ended;
            call.EndedAt = nowUtc;

            var creatorParticipant = await db.CallParticipants
                .FirstOrDefaultAsync(x => x.CallSessionId == call.Id && x.UserId == call.CreatedByUserId, ct);
            if (creatorParticipant is null)
            {
                creatorParticipant = new CallParticipant
                {
                    Id = Guid.NewGuid(),
                    CallSessionId = call.Id,
                    UserId = call.CreatedByUserId,
                    JoinedAt = call.StartedAt.Value,
                    BilledSeconds = 0
                };
                db.CallParticipants.Add(creatorParticipant);
            }

            creatorParticipant.LeftAt ??= nowUtc;
            creatorParticipant.BilledSeconds = billableSeconds;

            await quotaService.ApplyBilling(call.CreatedByUserId, month, billableSeconds, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        var usage = await db.MonthlyUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == call.CreatedByUserId && x.MonthYYYYMM == month, ct);

        return Results.Ok(new EndCallResponse
        {
            CallId = call.Id,
            BilledSeconds = billableSeconds,
            MonthYYYYMM = month,
            UsedSeconds = usage?.UsedSeconds ?? 0
        });
    }
}
