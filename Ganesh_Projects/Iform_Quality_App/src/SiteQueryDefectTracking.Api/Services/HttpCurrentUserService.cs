using System.Security.Claims;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Services;

/// <summary>
/// Current-user provider driven by the authenticated HTTP request. Registered after
/// AddInfrastructure so it wins the DI resolution order over the anonymous default.
/// </summary>
public class HttpCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("name");

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email");

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles
    {
        get
        {
            var claims = Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
                ?? new List<string>();
            return claims;
        }
    }

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public bool IsManager => IsInRole("Manager");

    public string? IpAddress
    {
        get
        {
            var http = accessor.HttpContext;
            if (http is null) return null;

            var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            return http.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? DeviceInfo => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}