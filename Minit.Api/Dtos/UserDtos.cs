using System.ComponentModel.DataAnnotations;

namespace Minit.Api.Dtos;

public sealed record RegisterUserRequest(
    [property: Required, StringLength(80, MinimumLength = 1)] string DisplayName);

public sealed record RegisterUserResponse(Guid UserId, string DisplayName, string Code, int MonthlyLimitSeconds, DateTime CreatedAtUtc);

public sealed record UserByCodeResponse(Guid UserId, string DisplayName);
