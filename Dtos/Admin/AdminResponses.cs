namespace Workspace.Dtos.Admin;

public sealed record UpdateUserLimitResponse(
    Guid UserId,
    int MonthlyLimitSeconds,
    int MonthlyLimitMinutes);

public sealed record CreateUsageAdjustmentResponse(
    Guid Id,
    Guid UserId,
    int MonthYYYYMM,
    int DeltaSeconds,
    string Reason,
    DateTime CreatedAt,
    int UsedSeconds);
