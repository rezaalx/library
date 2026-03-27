using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Minit.Api.Data;
using Minit.Api.Domain;
using Minit.Api.Dtos;
using Minit.Api.Services;

namespace Minit.Api.Endpoints;

public static class CallsEndpoints
{
    public static RouteGroupBuilder MapCallsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/calls").WithTags("Calls");

        group.MapPost("/start", StartCallAsync)
            .WithName("StartCall")
            .WithSummary("Start a call session")
            .Produces<StartCallResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{callId:guid}/join", JoinCallAsync)
            .WithName("JoinCall")
            .WithSummary("Join an active call session")
            .Produces<JoinCallResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{callId:guid}/end", EndCallAsync)
            .WithName("EndCall")
            .WithSummary("End a call session and bill creator usage")
            .Produces<EndCallResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Created<StartCallResponse>, ProblemHttpResult>> StartCallAsync(
        StartCallRequest request,
        AppDbContext dbContext,
        IQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        if (request.CreatedByUserId == request.CalleeUserId)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request",
                detail: "Caller and callee cannot be the same user.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_participants" });
        }

        var creator = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.CreatedByUserId && x.IsActive, cancellationToken);
        if (creator is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Creator not found",
                detail: "The caller user does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "creator_not_found" });
        }

        var callee = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.CalleeUserId && x.IsActive, cancellationToken);
        if (callee is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Callee not found",
                detail: "The callee user does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "callee_not_found" });
        }

        var now = DateTimeProvider.UtcNow;
        var remaining = await quotaService.CheckRemainingSecondsAsync(creator.Id, now, cancellationToken);
        if (remaining <= 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Quota exceeded",
                detail: "No remaining monthly call seconds.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "quota_exceeded" });
        }

        var session = new CallSession
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = creator.Id,
            Provider = string.IsNullOrWhiteSpace(request.Provider) ? "internal" : request.Provider.Trim(),
            ProviderRoomId = string.IsNullOrWhiteSpace(request.ProviderRoomId) ? null : request.ProviderRoomId.Trim(),
            Status = CallSessionStatus.Active,
            CreatedAt = now,
            StartedAt = now
        };

        var creatorParticipant = new CallParticipant
        {
            Id = Guid.NewGuid(),
            CallSessionId = session.Id,
            UserId = creator.Id,
            JoinedAt = now
        };

        dbContext.CallSessions.Add(session);
        dbContext.CallParticipants.Add(creatorParticipant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/calls/{session.Id}", new StartCallResponse(
            session.Id,
            creator.Id,
            callee.Id,
            session.Provider,
            session.ProviderRoomId,
            session.Status.ToString(),
            session.StartedAt,
            remaining));
    }

    private static async Task<Results<Ok<JoinCallResponse>, ProblemHttpResult>> JoinCallAsync(
        Guid callId,
        JoinCallRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.CallSessions
            .FirstOrDefaultAsync(x => x.Id == callId, cancellationToken);
        if (session is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Call not found",
                detail: "Call session was not found.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "call_not_found" });
        }

        if (session.Status is CallSessionStatus.Ended or CallSessionStatus.Cancelled)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid call state",
                detail: "Cannot join an ended or cancelled call.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_call_state" });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId && x.IsActive, cancellationToken);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: "User does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "user_not_found" });
        }

        var participant = await dbContext.CallParticipants
            .FirstOrDefaultAsync(x => x.CallSessionId == callId && x.UserId == request.UserId, cancellationToken);

        if (participant is null)
        {
            participant = new CallParticipant
            {
                Id = Guid.NewGuid(),
                CallSessionId = callId,
                UserId = request.UserId,
                JoinedAt = DateTimeProvider.UtcNow
            };
            dbContext.CallParticipants.Add(participant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new JoinCallResponse(
            participant.Id,
            participant.CallSessionId,
            participant.UserId,
            participant.JoinedAt));
    }

    private static async Task<Results<Ok<EndCallResponse>, ProblemHttpResult>> EndCallAsync(
        Guid callId,
        EndCallRequest request,
        AppDbContext dbContext,
        IQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.CallSessions
            .FirstOrDefaultAsync(x => x.Id == callId, cancellationToken);
        if (session is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Call not found",
                detail: "Call session was not found.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "call_not_found" });
        }

        var endedBy = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.EndedByUserId && x.IsActive, cancellationToken);
        if (endedBy is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: "EndedBy user does not exist or is inactive.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "user_not_found" });
        }

        if (session.Status == CallSessionStatus.Ended)
        {
            return TypedResults.Ok(new EndCallResponse(
                session.Id,
                session.Status.ToString(),
                session.EndedAt,
                0,
                session.CreatedByUserId));
        }

        var now = DateTimeProvider.UtcNow;
        var startedAt = session.StartedAt ?? session.CreatedAt;
        var durationSeconds = Math.Max(0, (int)Math.Ceiling((now - startedAt).TotalSeconds));

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        session.Status = CallSessionStatus.Ended;
        session.EndedAt = now;

        var creatorParticipant = await dbContext.CallParticipants
            .FirstOrDefaultAsync(x => x.CallSessionId == callId && x.UserId == session.CreatedByUserId, cancellationToken);
        if (creatorParticipant is null)
        {
            creatorParticipant = new CallParticipant
            {
                Id = Guid.NewGuid(),
                CallSessionId = callId,
                UserId = session.CreatedByUserId,
                JoinedAt = startedAt
            };
            dbContext.CallParticipants.Add(creatorParticipant);
        }

        creatorParticipant.LeftAt ??= now;
        creatorParticipant.BilledSeconds = durationSeconds;

        await quotaService.ApplyBillingAsync(session.CreatedByUserId, durationSeconds, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return TypedResults.Ok(new EndCallResponse(
            session.Id,
            session.Status.ToString(),
            session.EndedAt,
            durationSeconds,
            session.CreatedByUserId));
    }
}
