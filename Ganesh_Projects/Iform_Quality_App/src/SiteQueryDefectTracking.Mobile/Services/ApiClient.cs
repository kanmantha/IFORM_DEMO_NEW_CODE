using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SiteQueryDefectTracking.Mobile.Models;

namespace SiteQueryDefectTracking.Mobile.Services;

public static class ApiConfig
{
    public const string BaseUrl = "http://localhost:5170";

    public const string AccessTokenKey = "auth.access_token";
    public const string RefreshTokenKey = "auth.refresh_token";
    public const string ExpiresAtKey = "auth.expires_at";

    public const string PhotoRetentionDays = "30";
}

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(string message, int statusCode = 0, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private readonly HttpClient _http;
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? FullName { get; private set; }
    public string? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();

    public ApiClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(ApiConfig.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(60);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public async Task<bool> RestoreSessionAsync(CancellationToken ct = default)
    {
        var token = await SecureStorage.Default.GetAsync(ApiConfig.AccessTokenKey);
        var refresh = await SecureStorage.Default.GetAsync(ApiConfig.RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            AccessToken = null;
            RefreshToken = null;
            return false;
        }

        AccessToken = token;
        RefreshToken = refresh;
        return true;
    }

    public void ClearSession()
    {
        AccessToken = null;
        RefreshToken = null;
        FullName = null;
        UserId = null;
        Roles = Array.Empty<string>();
        SecureStorage.Default.Remove(ApiConfig.AccessTokenKey);
        SecureStorage.Default.Remove(ApiConfig.RefreshTokenKey);
        SecureStorage.Default.Remove(ApiConfig.ExpiresAtKey);
    }

    public async Task<bool> LoginAsync(string userNameOrEmail, string password, CancellationToken ct = default)
    {
        var payload = new { userNameOrEmail, password };
        var wrapper = await SendCoreAsync<TokenResponse>(
            HttpMethod.Post, "api/auth/login", payload, authenticated: false, allowRefresh: false, ct);
        var result = wrapper;
        if (result is null)
        {
            throw new ApiException("Login failed: no server response.");
        }

        if (!result.Success || result.Data is null)
        {
            throw new ApiException(result.Message ?? "Login failed. Check credentials.");
        }

        AccessToken = result.Data.AccessToken;
        RefreshToken = result.Data.RefreshToken;
        await SecureStorage.Default.SetAsync(ApiConfig.AccessTokenKey, AccessToken);
        await SecureStorage.Default.SetAsync(ApiConfig.RefreshTokenKey, RefreshToken);

        await LoadCurrentUserAsync(ct);
        return true;
    }

    public async Task LoadCurrentUserAsync(CancellationToken ct = default)
    {
        try
        {
            var me = await GetAsync<CurrentUser>("api/auth/me", ct);
            if (me is not null)
            {
                FullName = me.FullName;
                UserId = me.Id;
                Roles = me.Roles ?? new List<string>();
            }
        }
        catch (Exception)
        {
            // Non-fatal: session still usable with token only.
        }
    }

    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    public Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
        => SendCoreAsync<T>(HttpMethod.Get, path, null, authenticated: true, allowRefresh: true, ct)
            .ContinueWith(t => ExtractData(t.Result), ct);

    public Task<T?> PostAsync<T>(string path, object? payload = null, CancellationToken ct = default)
        => SendCoreAsync<T>(HttpMethod.Post, path, payload, authenticated: true, allowRefresh: true, ct)
            .ContinueWith(t => ExtractData(t.Result), ct);

    public Task<T?> PutAsync<T>(string path, object? payload = null, CancellationToken ct = default)
        => SendCoreAsync<T>(HttpMethod.Put, path, payload, authenticated: true, allowRefresh: true, ct)
            .ContinueWith(t => ExtractData(t.Result), ct);

    public async Task PostVoidAsync(string path, object? payload = null, CancellationToken ct = default)
    {
        var result = await SendCoreAsync<object?>(HttpMethod.Post, path, payload, authenticated: true, allowRefresh: true, ct);
        if (result is null)
        {
            throw new ApiException("No server response.");
        }
        if (!result.Success)
        {
            throw new ApiException(result.Message ?? "Request failed.");
        }
    }

    public async Task<byte[]> DownloadAsync(string url, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ResolveUrl(url));
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException($"Download failed ({response.StatusCode}).", (int)response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<T?> UploadAsync<T>(string path, string filePath, string fileName, string contentType, CancellationToken ct = default)
        where T : class
    {
        using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, ResolveUrl(path)) { Content = content };
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException($"Upload failed ({response.StatusCode}): {json}", (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
        return ExtractData(result);
    }

    private string ResolveUrl(string url)
        => url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(ApiConfig.BaseUrl)
            ? url
            : $"{ApiConfig.BaseUrl.TrimEnd('/')}/{url.TrimStart('/')}";

    private static T2? ExtractData<T2>(ApiResponse<T2>? response)
    {
        if (response is null) return default;
        if (!response.Success) throw new ApiException(response.Message ?? "Request failed.");
        return response.Data;
    }

    private async Task<ApiResponse<T>?> SendCoreAsync<T>(
        HttpMethod method, string path, object? payload, bool authenticated, bool allowRefresh, CancellationToken ct)
    {
        if (authenticated && string.IsNullOrWhiteSpace(AccessToken))
        {
            throw new ApiException("Not authenticated.");
        }

        var response = await SendRawAsync(method, path, payload, authenticated, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && allowRefresh
            && !string.IsNullOrWhiteSpace(RefreshToken))
        {
            if (await TryRefreshAsync(ct))
            {
                response.Dispose();
                response = await SendRawAsync(method, path, payload, authenticated: true, ct);
            }
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException($"Request failed ({response.StatusCode}): {json}", (int)response.StatusCode);
        }

        try
        {
            return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Invalid server response: {ex.Message}");
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method, string path, object? payload, bool authenticated, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, ResolveUrl(path));
        if (authenticated && !string.IsNullOrWhiteSpace(AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        return await _http.SendAsync(request, ct);
    }

    internal async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(RefreshToken)) return false;

        var payload = new { refreshToken = RefreshToken };
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ResolveUrl("api/auth/refresh"))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            ClearSession();
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ApiResponse<TokenResponse>>(json, JsonOptions);
        if (result?.Data is null)
        {
            ClearSession();
            return false;
        }

        AccessToken = result.Data.AccessToken;
        RefreshToken = result.Data.RefreshToken;
        await SecureStorage.Default.SetAsync(ApiConfig.AccessTokenKey, AccessToken);
        await SecureStorage.Default.SetAsync(ApiConfig.RefreshTokenKey, RefreshToken);
        return true;
    }
}