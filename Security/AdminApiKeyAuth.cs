namespace Workspace.Security;

public static class AdminApiKeyAuth
{
    public const string HeaderName = "X-Admin-Key";

    public static bool IsAuthorized(HttpRequest request, IConfiguration configuration)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return false;
        }

        var configuredKey = configuration["Admin:ApiKey"];
        return !string.IsNullOrWhiteSpace(configuredKey) && string.Equals(headerValue.ToString(), configuredKey, StringComparison.Ordinal);
    }
}
