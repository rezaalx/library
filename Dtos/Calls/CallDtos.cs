using System.ComponentModel.DataAnnotations;
using Workspace.Domain;

namespace Workspace.Dtos.Calls;

public sealed class StartCallRequest
{
    [Required]
    public Guid CreatedByUserId { get; init; }

    [Required]
    public Guid CalleeUserId { get; init; }

    [StringLength(40)]
    public string? Provider { get; init; }

    [StringLength(200)]
    public string? ProviderRoomId { get; init; }
}

public sealed class StartCallResponse
{
    public Guid CallId { get; init; }
    public CallSessionStatus Status { get; init; }
    public DateTime StartedAt { get; init; }
}

public sealed class JoinCallRequest
{
    [Required]
    public Guid UserId { get; init; }
}

public sealed class JoinCallResponse
{
    public Guid CallId { get; init; }
    public Guid UserId { get; init; }
    public DateTime JoinedAt { get; init; }
    public bool AlreadyJoined { get; init; }
}

public sealed class EndCallRequest
{
    [Required]
    public Guid EndedByUserId { get; init; }
}

public sealed class EndCallResponse
{
    public Guid CallId { get; init; }
    public int BilledSeconds { get; init; }
    public int MonthYYYYMM { get; init; }
    public int UsedSeconds { get; init; }
}
