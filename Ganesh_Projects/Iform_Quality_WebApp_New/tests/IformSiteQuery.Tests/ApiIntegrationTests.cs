using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IformSiteQuery.Domain.Entities;
using IformSiteQuery.Domain.Enums;
using IformSiteQuery.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IformSiteQuery.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = CreateClient();
        var token = await LoginAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(body, JsonOpts);
        Assert.NotNull(envelope?.Data?.Token);
        return envelope!.Data!.Token;
    }

    [Fact]
    public async Task Anonymous_ApiQueries_ReturnsUnauthorized()
    {
        var response = await CreateClient().GetAsync("/api/queries");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_ApiDashboard_ReturnsUnauthorized()
    {
        var response = await CreateClient().GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Manager_ReturnsTokenAndRole()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "manager@iform.co.in", Password = "Manager@123" });
        response.EnsureSuccessStatusCode();
        var envelope = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        Assert.True(envelope?.Success);
        Assert.NotNull(envelope!.Data!.Token);
        Assert.Equal("Manager", envelope.Data.User.Role);
        Assert.Equal("manager@iform.co.in", envelope.Data.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "engineer@iform.co.in", Password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUser()
    {
        var client = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var envelope = JsonSerializer.Deserialize<ApiResponse<UserDto>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.Equal("SiteEngineer", envelope!.Data!.Role);
    }

    [Fact]
    public async Task Manager_SearchQueries_ReturnsSeededData()
    {
        var client = await CreateAuthenticatedClientAsync("manager@iform.co.in", "Manager@123");
        var response = await client.GetAsync("/api/queries");
        response.EnsureSuccessStatusCode();
        var envelope = JsonSerializer.Deserialize<ApiResponse<PagedQueryResult>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.NotNull(envelope!.Data);
        Assert.True(envelope.Data.Items.Count > 0, "Seeded queries should be visible to the manager.");
    }

    [Fact]
    public async Task SiteEngineer_SearchIsScopedToOwnQueries()
    {
        var manager = await CreateAuthenticatedClientAsync("manager@iform.co.in", "Manager@123");
        var engineer = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");

        var managerIpo = $"TST-MGR-{Guid.NewGuid():N}"[..20];
        var engineerIpo = $"TST-ENG-{Guid.NewGuid():N}"[..20];

        await manager.PostAsJsonAsync("/api/queries", new CreateQueryRequest
        {
            IpoNumber = managerIpo,
            ProjectId = 1,
            IssueType = IssueType.Missing,
            QtyNos = 10,
            QtySqm = 0
        });

        await engineer.PostAsJsonAsync("/api/queries", new CreateQueryRequest
        {
            IpoNumber = engineerIpo,
            ProjectId = 1,
            IssueType = IssueType.DesignMistake,
            QtyNos = 5,
            QtySqm = 0
        });

        var managerSearch = await engineer.GetAsync($"/api/queries?search={managerIpo}");
        var managerSearchEnvelope = JsonSerializer.Deserialize<ApiResponse<PagedQueryResult>>(
            await managerSearch.Content.ReadAsStringAsync(), JsonOpts);
        Assert.Equal(0, managerSearchEnvelope!.Data!.Total);

        var ownSearch = await engineer.GetAsync($"/api/queries?search={engineerIpo}");
        var ownSearchEnvelope = JsonSerializer.Deserialize<ApiResponse<PagedQueryResult>>(
            await ownSearch.Content.ReadAsStringAsync(), JsonOpts);
        Assert.Equal(1, ownSearchEnvelope!.Data!.Total);
    }

    [Fact]
    public async Task CreateQuery_ReturnsQueryNumber()
    {
        var client = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var response = await client.PostAsJsonAsync("/api/queries", new CreateQueryRequest
        {
            IpoNumber = $"IPO-{Guid.NewGuid():N}"[..20],
            ProjectId = 1,
            IssueType = IssueType.ProductionMistake,
            QtyNos = 25,
            QtySqm = 12.5m,
            Description = "Integration test query",
            SlabTargetDate = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = JsonSerializer.Deserialize<ApiResponse<QueryDto>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.StartsWith("QRY-", envelope!.Data!.QueryNumber);
        Assert.Equal(QueryStatus.Pending, envelope.Data.Status);
        Assert.True(envelope.Data.Id > 0);
    }

    [Fact]
    public async Task CreateQuery_MissingIpo_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var response = await client.PostAsJsonAsync("/api/queries", new CreateQueryRequest
        {
            IpoNumber = "",
            ProjectId = 1,
            IssueType = IssueType.Missing,
            QtyNos = 1,
            QtySqm = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SiteEngineer_Resolve_ReturnsForbidden()
    {
        var engineer = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var created = await engineer.PostAsJsonAsync("/api/queries", new CreateQueryRequest
        {
            IpoNumber = $"RBA-{Guid.NewGuid():N}"[..20],
            ProjectId = 1,
            IssueType = IssueType.Missing,
            QtyNos = 3,
            QtySqm = 0
        });
        var createdEnvelope = JsonSerializer.Deserialize<ApiResponse<QueryDto>>(
            await created.Content.ReadAsStringAsync(), JsonOpts);

        var resolve = await engineer.PostAsJsonAsync($"/api/queries/{createdEnvelope!.Data!.Id}/resolve",
            new ResolveRequest { ResolutionNote = "should not work" });
        Assert.Equal(HttpStatusCode.Forbidden, resolve.StatusCode);
    }

    [Fact]
    public async Task Manager_Resolve_ChangesStatusToResolved()
    {
        var engineer = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var created = await engineer.PostAsJsonAsync("/api/queries", new CreateQueryRequest
        {
            IpoNumber = $"RES-{Guid.NewGuid():N}"[..20],
            ProjectId = 1,
            IssueType = IssueType.DispatchMissing,
            QtyNos = 8,
            QtySqm = 0
        });
        var createdEnvelope = JsonSerializer.Deserialize<ApiResponse<QueryDto>>(
            await created.Content.ReadAsStringAsync(), JsonOpts);
        var queryId = createdEnvelope!.Data!.Id;

        var manager = await CreateAuthenticatedClientAsync("manager@iform.co.in", "Manager@123");
        var resolve = await manager.PostAsJsonAsync($"/api/queries/{queryId}/resolve",
            new ResolveRequest { ResolutionNote = "Verified and closed." });
        resolve.EnsureSuccessStatusCode();

        var envelope = JsonSerializer.Deserialize<ApiResponse<QueryDto>>(
            await resolve.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.Equal(QueryStatus.Resolved, envelope!.Data!.Status);
        Assert.Equal("Verified and closed.", envelope.Data.ResolutionNote);

        var detail = await manager.GetAsync($"/api/queries/{queryId}");
        var detailEnvelope = JsonSerializer.Deserialize<ApiResponse<QueryDto>>(
            await detail.Content.ReadAsStringAsync(), JsonOpts);
        Assert.Equal(QueryStatus.Resolved, detailEnvelope!.Data!.Status);
    }

    [Fact]
    public async Task Dashboard_ManagerOnly_ForbidsSiteEngineer()
    {
        var engineer = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var response = await engineer.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var manager = await CreateAuthenticatedClientAsync("manager@iform.co.in", "Manager@123");
        var managerResponse = await manager.GetAsync("/api/dashboard");
        managerResponse.EnsureSuccessStatusCode();
        var envelope = JsonSerializer.Deserialize<ApiResponse<DashboardDto>>(
            await managerResponse.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.NotNull(envelope!.Data);
    }

    [Fact]
    public async Task Reference_Projects_ReturnsSeededProjects()
    {
        var client = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var response = await client.GetAsync("/api/reference/projects");
        response.EnsureSuccessStatusCode();
        var envelope = JsonSerializer.Deserialize<ApiResponse<List<Project>>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.True(envelope!.Data!.Count >= 10, "Seeded projects should be listed.");
    }

    [Fact]
    public async Task ProductsApi_Search_FiltersByCode()
    {
        var client = await CreateAuthenticatedClientAsync("engineer@iform.co.in", "Engineer@123");
        var response = await client.GetAsync("/api/products?q=DCAA0001");
        response.EnsureSuccessStatusCode();
        var envelope = JsonSerializer.Deserialize<ApiResponse<List<ProductDto>>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.True(envelope?.Success);
        Assert.NotNull(envelope!.Data);
        Assert.NotEmpty(envelope.Data);
        Assert.All(envelope.Data, p =>
            Assert.Contains("DCAA0001", p.Code, StringComparison.OrdinalIgnoreCase));
    }
}
