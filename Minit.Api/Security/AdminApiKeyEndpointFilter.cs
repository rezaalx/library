using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Minit.Api.Security;

public sealed class AdminApiKeyEndpointFilter(IOptions<AdminOptions> adminOptions) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(AdminApiKeyConstants.HeaderName, out var apiKeyHeader))
        {
            return ValueTask.FromResult<object?>(
                Results.Problem(
                    title: "Unauthorized",
                    detail: "Missing admin API key.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "admin_key_missing" }));
        }

        var expectedKey = adminOptions.Value.ApiKey;
        var providedKey = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return ValueTask.FromResult<object?>(
                Results.Problem(
                    title: "Unauthorized",
                    detail: "Admin API key is not configured.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "admin_key_not_configured" }));
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        if (expectedBytes.Length != providedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return ValueTask.FromResult<object?>(
                Results.Problem(
                    title: "Unauthorized",
                    detail: "Invalid admin API key.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "admin_key_invalid" }));
        }

        return next(context);
    }
}
