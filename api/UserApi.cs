using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Api
{
    public sealed class ApiException : Exception
    {
        public int Status { get; }
        public string? Body { get; }

        public ApiException(int status, string message, string? body = null) : base(message)
        {
            Status = status;
            Body = body;
        }
    }

    public sealed class UserApiConfig
    {
        public string BaseUrl { get; }
        public string? AuthToken { get; }
        public TimeSpan Timeout { get; }
        public IDictionary<string, string>? DefaultHeaders { get; }
        public bool VerifySsl { get; }

        public UserApiConfig(
            string baseUrl,
            string? authToken = null,
            TimeSpan? timeout = null,
            IDictionary<string, string>? defaultHeaders = null,
            bool verifySsl = true)
        {
            BaseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
            AuthToken = authToken;
            Timeout = timeout ?? TimeSpan.FromSeconds(30);
            DefaultHeaders = defaultHeaders;
            VerifySsl = verifySsl;
        }
    }

    public sealed class UserApi : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownsClient;
        private readonly UserApiConfig _config;

        public UserApi(UserApiConfig config, HttpClient? httpClient = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (httpClient is null)
            {
                var handler = new HttpClientHandler();
                if (!config.VerifySsl)
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                }
                _httpClient = new HttpClient(handler);
                _ownsClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _ownsClient = false;
            }

            _httpClient.Timeout = config.Timeout;
        }

        public void Dispose()
        {
            if (_ownsClient)
            {
                _httpClient.Dispose();
            }
        }

        private static string BuildQueryString(IDictionary<string, object?>? query)
        {
            if (query == null || query.Count == 0)
            {
                return string.Empty;
            }
            var sb = new StringBuilder();
            var first = true;
            foreach (var kv in query)
            {
                var key = kv.Key;
                var value = kv.Value;
                if (value is null) continue;

                if (value is IEnumerable<object> enumerable && value is not string)
                {
                    foreach (var item in enumerable)
                    {
                        AppendParam(sb, ref first, key, item);
                    }
                }
                else
                {
                    AppendParam(sb, ref first, key, value);
                }
            }
            return sb.ToString();
        }

        private static void AppendParam(StringBuilder sb, ref bool first, string key, object? value)
        {
            if (!first)
            {
                sb.Append('&');
            }
            else
            {
                sb.Append('?');
                first = false;
            }
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value?.ToString() ?? string.Empty));
        }

        private string BuildUrl(string path, IDictionary<string, object?>? query = null)
        {
            if (!path.StartsWith('/')) path = "/" + path;
            var url = _config.BaseUrl + path;
            var qs = BuildQueryString(query);
            return url + qs;
        }

        private HttpRequestMessage BuildRequest(string method, string url, JsonNode? body = null, IDictionary<string, string>? headers = null)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Headers
            if (_config.DefaultHeaders != null)
            {
                foreach (var kv in _config.DefaultHeaders)
                {
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }
            if (!string.IsNullOrWhiteSpace(_config.AuthToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AuthToken);
            }
            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            if (body != null)
            {
                var json = body.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.Content == null) return null;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(text)) return null;
            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return JsonNode.Parse(text);
                }
                catch
                {
                    return null;
                }
            }
            return new JsonObject { ["text"] = text };
        }

        private async Task<(int Status, JsonNode? Data, HttpResponseMessage Response)> RequestAsync(
            string method,
            string path,
            IDictionary<string, object?>? query = null,
            JsonNode? body = null,
            IDictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(path, query);
            using var request = BuildRequest(method, url, body, headers);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException($"Request timed out after {_httpClient.Timeout.TotalMilliseconds} ms: {method.ToUpperInvariant()} {url}");
            }

            var status = (int)response.StatusCode;
            var data = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

            if (status < 200 || status >= 300)
            {
                var bodyText = data is JsonObject obj && obj.TryGetPropertyValue("text", out var t) ? t?.ToString() : data?.ToJsonString();
                throw new ApiException(status, $"HTTP {status} for {method.ToUpperInvariant()} {url}", bodyText);
            }

            return (status, data, response);
        }

        // Public API

        public Task<JsonNode?> ListUsersAsync(int? page = null, int? perPage = null, IDictionary<string, object?>? filters = null, CancellationToken cancellationToken = default)
        {
            var query = new Dictionary<string, object?>();
            if (page.HasValue) query["page"] = page.Value;
            if (perPage.HasValue) query["per_page"] = perPage.Value;
            if (filters != null)
            {
                foreach (var kv in filters) query[kv.Key] = kv.Value;
            }
            return SendAndReturnJsonAsync("GET", "/users", query, null, null, cancellationToken);
        }

        public Task<JsonNode?> GetUserAsync(object userId, CancellationToken cancellationToken = default)
        {
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            return SendAndReturnJsonAsync("GET", $"/users/{userId}", null, null, null, cancellationToken);
        }

        public Task<JsonNode?> CreateUserAsync(JsonNode user, CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return SendAndReturnJsonAsync("POST", "/users", null, user, null, cancellationToken);
        }

        public Task<JsonNode?> UpdateUserAsync(object userId, JsonNode user, CancellationToken cancellationToken = default)
        {
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            if (user == null) throw new ArgumentNullException(nameof(user));
            return SendAndReturnJsonAsync("PUT", $"/users/{userId}", null, user, null, cancellationToken);
        }

        public async Task<bool> DeleteUserAsync(object userId, CancellationToken cancellationToken = default)
        {
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            var (status, _, _) = await RequestAsync("DELETE", $"/users/{userId}", null, null, null, cancellationToken).ConfigureAwait(false);
            return status == 204 || (status >= 200 && status < 300);
        }

        private async Task<JsonNode?> SendAndReturnJsonAsync(string method, string path, IDictionary<string, object?>? query, JsonNode? body, IDictionary<string, string>? headers, CancellationToken cancellationToken)
        {
            var (_, data, _) = await RequestAsync(method, path, query, body, headers, cancellationToken).ConfigureAwait(false);
            return data;
        }
    }
}

