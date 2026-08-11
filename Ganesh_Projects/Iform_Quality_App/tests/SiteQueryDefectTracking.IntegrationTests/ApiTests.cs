using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SiteQueryDefectTracking.IntegrationTests;

public class AuthApiTests : IClassFixture<SiteQueryDefectTrackingApiFactory>
{
    private readonly SiteQueryDefectTrackingApiFactory _factory;

    public AuthApiTests(SiteQueryDefectTrackingApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        using var client = _factory.CreateClient();
        var token = await SiteQueryDefectTrackingApiFactory.LoginAsync(client, "manager@demo.local", "Demo@1234!");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Theory]
    [InlineData("manager@demo.local", "wrong-password")]
    [InlineData("nobody@demo.local", "Demo@1234!")]
    public async Task Login_InvalidCredentials_Returns401(string email, string password)
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new { userNameOrEmail = email, password });
        var response = await client.PostAsync("/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsUser()
    {
        var (client, _) = await _factory.CreateAuthenticatedClientAsync("manager@demo.local");
        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var email = json.RootElement.GetProperty("data").GetProperty("email").GetString();
        Assert.Equal("manager@demo.local", email?.ToLowerInvariant());
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        using var client = _factory.CreateClient();

        var loginBody = JsonSerializer.Serialize(new { userNameOrEmail = "manager@demo.local", password = "Demo@1234!" });
        var login = await client.PostAsync("/api/auth/login", new StringContent(loginBody, Encoding.UTF8, "application/json"));
        var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var refresh1 = loginJson.RootElement.GetProperty("data").GetProperty("refreshToken").GetString();

        var refreshBody = JsonSerializer.Serialize(new { refreshToken = refresh1 });
        var response = await client.PostAsync("/api/auth/refresh", new StringContent(refreshBody, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var refreshJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var refresh2 = refreshJson.RootElement.GetProperty("data").GetProperty("refreshToken").GetString();
        Assert.NotEqual(refresh1, refresh2);

        var reuse = await client.PostAsync("/api/auth/refresh", new StringContent(refreshBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class QueryApiTests : IClassFixture<SiteQueryDefectTrackingApiFactory>
{
    private readonly SiteQueryDefectTrackingApiFactory _factory;

    public QueryApiTests(SiteQueryDefectTrackingApiFactory factory) => _factory = factory;

    private static Guid FirstId(JsonElement page) =>
        page.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetGuid();

    private async Task<(Guid ProjectId, Guid IssueTypeId)> GetLookupsAsync(HttpClient client)
    {
        var proj = await client.GetAsync("/api/projects/active");
        proj.EnsureSuccessStatusCode();
        var projJson = JsonDocument.Parse(await proj.Content.ReadAsStringAsync());
        var projectId = projJson.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid();

        var search = await client.GetAsync("/api/queries?pageSize=1");
        search.EnsureSuccessStatusCode();
        var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        var issueTypeId = searchJson.RootElement.GetProperty("data").GetProperty("items")[0]
            .GetProperty("issueTypeId").GetGuid();

        return (projectId, issueTypeId);
    }

    private async Task<(Guid ProjectId, Guid IssueTypeId)> GetLookupsAsync()
    {
        var (authClient, _) = await _factory.CreateAuthenticatedClientAsync();
        using var _ = authClient;
        return await GetLookupsAsync(authClient);
    }

    [Fact]
    public async Task CreateQuery_AsManager_ReturnsNewId()
    {
        var (projectId, issueTypeId) = await GetLookupsAsync();
        var (authClient, _) = await _factory.CreateAuthenticatedClientAsync();

        var body = JsonSerializer.Serialize(new
        {
            projectId = projectId.ToString(),
            issueTypeId = issueTypeId.ToString(),
            ipo = $"IPO-IT-{Guid.NewGuid():N}"[..20],
            quantityNos = 10,
            quantitySqm = 15.5m,
            description = "Integration test query"
        });

        var response = await authClient.PostAsync("/api/queries", new StringContent(body, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newId = json.RootElement.GetProperty("data").GetGuid();

        Assert.NotEqual(Guid.Empty, newId);
    }

    [Fact]
    public async Task CreateQuery_MissingIpo_Returns400()
    {
        var (projectId, issueTypeId) = await GetLookupsAsync();
        var (authClient, _) = await _factory.CreateAuthenticatedClientAsync();

        var body = JsonSerializer.Serialize(new
        {
            projectId = projectId.ToString(),
            issueTypeId = issueTypeId.ToString(),
            quantityNos = 1,
            quantitySqm = 1m
        });
        var response = await authClient.PostAsync("/api/queries", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateQuery_WithoutAuth_Returns401()
    {
        using var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new { projectId = Guid.NewGuid(), issueTypeId = Guid.NewGuid(), ipo = "X", quantityNos = 1, quantitySqm = 1m });
        var response = await client.PostAsync("/api/queries", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Engineer_CannotSeeOtherEngineersQuery()
    {
        // engine2 raises a query
        var (projectId, issueTypeId) = await GetLookupsAsync();
        var (eng2Client, _) = await _factory.CreateAuthenticatedClientAsync("engineer2@demo.local");

        var body = JsonSerializer.Serialize(new
        {
            projectId = projectId.ToString(),
            issueTypeId = issueTypeId.ToString(),
            ipo = $"IPO-IDOR-{Guid.NewGuid():N}"[..20],
            quantityNos = 2,
            quantitySqm = 3m
        });
        var create = await eng2Client.PostAsync("/api/queries", new StringContent(body, Encoding.UTF8, "application/json"));
        create.EnsureSuccessStatusCode();
        var queryId = JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetGuid();

        // engineer1 must not be able to read or comment
        var (eng1Client, _) = await _factory.CreateAuthenticatedClientAsync("siteengineer@demo.local");
        var read = await eng1Client.GetAsync($"/api/queries/{queryId}");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        var comment = JsonSerializer.Serialize(new { commentText = "intruder" });
        var postComment = await eng1Client.PostAsync($"/api/queries/{queryId}/comments",
            new StringContent(comment, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, postComment.StatusCode);
    }

    [Fact]
    public async Task Engineer_DashboardRestricted_To403()
    {
        var (engClient, _) = await _factory.CreateAuthenticatedClientAsync("siteengineer@demo.local");
        var response = await engClient.GetAsync("/api/dashboard/open");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Engineer_CannotCreateProduct()
    {
        var (engClient, _) = await _factory.CreateAuthenticatedClientAsync("siteengineer@demo.local");
        var body = JsonSerializer.Serialize(new { code = "X", description = "Y" });
        var response = await engClient.PostAsync("/api/products", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResolveQuery_RecordsPendingToResolvedHistory()
    {
        var (projectId, issueTypeId) = await GetLookupsAsync();
        var (manager, _) = await _factory.CreateAuthenticatedClientAsync();

        var body = JsonSerializer.Serialize(new
        {
            projectId = projectId.ToString(),
            issueTypeId = issueTypeId.ToString(),
            ipo = $"IPO-RO-{Guid.NewGuid():N}"[..20],
            quantityNos = 1,
            quantitySqm = 2m
        });
        var create = await manager.PostAsync("/api/queries", new StringContent(body, Encoding.UTF8, "application/json"));
        create.EnsureSuccessStatusCode();
        var queryId = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetGuid();

        var resolve = JsonSerializer.Serialize(new { resolutionNote = "Fixed on site" });
        var resolveResp = await manager.PutAsync($"/api/queries/{queryId}/resolve",
            new StringContent(resolve, Encoding.UTF8, "application/json"));
        resolveResp.EnsureSuccessStatusCode();

        var detail = await manager.GetAsync($"/api/queries/{queryId}");
        detail.EnsureSuccessStatusCode();
        var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var history = detailJson.RootElement.GetProperty("data").GetProperty("statusHistory");

        var last = history.EnumerateArray().FirstOrDefault(h =>
            h.GetProperty("toStatus").GetString() == "Resolved");
        Assert.Equal("Pending", last.GetProperty("fromStatus").GetString());

        // resolved query: further comments rejected
        var comment = JsonSerializer.Serialize(new { commentText = "too late" });
        var postComment = await manager.PostAsync($"/api/queries/{queryId}/comments",
            new StringContent(comment, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, postComment.StatusCode);
    }

    [Fact]
    public async Task Report_InvalidType_Returns400()
    {
        var (manager, _) = await _factory.CreateAuthenticatedClientAsync();
        var body = JsonSerializer.Serialize(new { type = 99, format = "Csv" });
        var response = await manager.PostAsync("/api/reports/generate", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_ReversedDateRange_Returns400()
    {
        var (manager, _) = await _factory.CreateAuthenticatedClientAsync();
        var body = JsonSerializer.Serialize(new
        {
            type = "OpenQueries",
            format = "Csv",
            from = "2026-08-01T00:00:00Z",
            to = "2020-01-01T00:00:00Z"
        });
        var response = await manager.PostAsync("/api/reports/generate", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_ValidOpenCsv_Returns200Csv()
    {
        var (manager, _) = await _factory.CreateAuthenticatedClientAsync();
        var body = JsonSerializer.Serialize(new { type = "OpenQueries", format = "Csv" });
        var response = await manager.PostAsync("/api/reports/generate", new StringContent(body, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        var contentType = data.GetProperty("contentType").GetString();
        Assert.Equal("text/csv", contentType);

        var content = Convert.FromBase64String(data.GetProperty("content").GetString()!);
        var text = Encoding.UTF8.GetString(content).TrimStart('\uFEFF');
        Assert.StartsWith("Query No", text);
    }
}