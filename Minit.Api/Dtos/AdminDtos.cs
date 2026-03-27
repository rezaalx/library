using System.ComponentModel.DataAnnotations;

namespace Minit.Api.Dtos;

public sealed record AdminSetUserLimitRequestDto(
    [property: Range(1, int.MaxValue)] int? Minutes,
    [property: Range(1, int.MaxValue)] int? Seconds);

public sealed record UserLimitResponseDto(
    Guid UserId,
    int MonthlyLimitSeconds,
    int MonthlyLimitMinutes);

public sealed record CreateUsageAdjustmentRequestDto(
    [property: Required] Guid UserId,
    [property: Range(190001, 999912)] int MonthYYYYMM,
    int DeltaSeconds,
    [property: Required, StringLength(200, MinimumLength = 1)] string Reason);

public sealed record UsageAdjustmentResponseDto(
    Guid Id,
    Guid UserId,
    int MonthYYYYMM,
    int DeltaSeconds,
    string Reason,
    DateTime CreatedAtUtc,
    int NewUsedSeconds);
