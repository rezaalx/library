namespace Minit.Api.Security;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string ApiKey { get; init; } = string.Empty;
}
