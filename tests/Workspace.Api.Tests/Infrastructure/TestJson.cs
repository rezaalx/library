using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workspace.Api.Tests.Infrastructure;

public static class TestJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> ReadRequiredAsync<T>(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return payload ?? throw new InvalidOperationException($"Expected JSON payload for type {typeof(T).Name}.");
    }

    public static void AssertStatus(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
    }
}
