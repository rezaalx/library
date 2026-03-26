using System.ComponentModel.DataAnnotations;

namespace Workspace.Dtos.Usage;

public sealed record UsageResponse(Guid UserId, int MonthYYYYMM, int UsedSeconds, int RemainingSeconds, int MonthlyLimitSeconds, DateTime UpdatedAt);

public sealed class UsageAdjustmentRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Range(200001, 299912)]
    public int MonthYYYYMM { get; init; }

    public int DeltaSeconds { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record UsageAdjustmentResponse(Guid Id, Guid UserId, int MonthYYYYMM, int DeltaSeconds, string Reason, DateTime CreatedAt);
