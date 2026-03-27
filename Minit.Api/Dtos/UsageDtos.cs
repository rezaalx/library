namespace Minit.Api.Dtos;

public sealed record MonthlyUsageResponse(
    Guid UserId,
    int MonthYYYYMM,
    int MonthlyLimitSeconds,
    int UsedSeconds,
    int RemainingSeconds,
    DateTime UpdatedAtUtc);
