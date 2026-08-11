namespace IformSiteQuery.Infrastructure.Auth;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "IformSiteQuery";
    public string Audience { get; set; } = "IformSiteQueryClients";
    public int ExpiryMinutes { get; set; } = 480;
}
