using System.ComponentModel.DataAnnotations;

namespace Workspace.Dtos.Users;

public sealed class RegisterUserRequest
{
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class RegisterUserResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int MonthlyLimitSeconds { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class UserByCodeResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
