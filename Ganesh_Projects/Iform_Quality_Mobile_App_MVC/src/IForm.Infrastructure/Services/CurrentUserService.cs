using System.Security.Claims;
using IForm.Contracts;
using IForm.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace IForm.Infrastructure.Services;

/// <summary>Resolves the current user + tenant context from the ASP.NET claims principal.</summary>
public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = Principal?.FindFirstValue("TenantId");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => Principal?.Identity?.Name;

    public string? FullName => Principal?.FindFirstValue("FullName");

    public IEnumerable<string> Roles => Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Array.Empty<string>();

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public string? IpAddress => _accessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public string? UserAgent => _accessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
}
