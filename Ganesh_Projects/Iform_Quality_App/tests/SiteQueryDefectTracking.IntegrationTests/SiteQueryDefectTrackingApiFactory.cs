using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SiteQueryDefectTracking.IntegrationTests;

public class SiteQueryDefectTrackingApiFactory : WebApplicationFactory<Program>
{
    public const string BaseAddress = "http://localhost";

    public const string ManagerEmail = "manager@demo.local";
    public const string EngineerEmail = "siteengineer@demo.local";
    public const string Engineer2Email = "engineer2@demo.local";
    public const string Password = "Demo@1234!";

    private const string TestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SiteQueryDefectTrackingQA;Integrated Security=True;TrustServerCertificate=True;";

    static SiteQueryDefectTrackingApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString
            });
        });
    }

    public async Task<(HttpClient Client, string AccessToken)> CreateAuthenticatedClientAsync(
        string email = ManagerEmail, string password = Password)
    {
        var client = CreateClient();
        var token = await LoginAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new { userNameOrEmail = email, password });
        var response = await client.PostAsync("/api/auth/login",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }
}