namespace SiteQueryDefectTracking.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsInRole(string role);
    bool IsManager { get; }
    string? IpAddress { get; }

    /// <summary>Device / user-agent information captured from the request when available.</summary>
    string? DeviceInfo { get; }
}