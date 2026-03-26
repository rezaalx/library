using System.ComponentModel.DataAnnotations;

namespace Workspace.Dtos.Admin;

public sealed record UpdateUserLimitRequest(
    int? MonthlyLimitSeconds,
    int? MonthlyLimitMinutes);

public sealed record UsageAdjustmentRequest(
    [Required] Guid UserId,
    [Range(200001, 999912)] int MonthYYYYMM,
    int DeltaSeconds,
    [Required, StringLength(200)] string Reason);
