using System.ComponentModel.DataAnnotations;

namespace Minit.Api.Dtos;

public sealed record StartCallRequest(
    [property: Required] Guid CreatedByUserId,
    [property: Required] Guid CalleeUserId,
    [property: MaxLength(40)] string? Provider,
    [property: MaxLength(200)] string? ProviderRoomId);

public sealed record StartCallResponse(
    Guid CallId,
    Guid CreatedByUserId,
    Guid CalleeUserId,
    string Provider,
    string? ProviderRoomId,
    string Status,
    DateTime? StartedAtUtc,
    int RemainingSecondsBeforeStart);

public sealed record JoinCallRequest([property: Required] Guid UserId);

public sealed record JoinCallResponse(
    Guid ParticipantId,
    Guid CallSessionId,
    Guid UserId,
    DateTime JoinedAtUtc);

public sealed record EndCallRequest([property: Required] Guid EndedByUserId);

public sealed record EndCallResponse(
    Guid CallId,
    string Status,
    DateTime? EndedAtUtc,
    int BilledSecondsCreator,
    Guid? BilledUserId);
