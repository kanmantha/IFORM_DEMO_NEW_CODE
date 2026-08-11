using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Contracts;

namespace SiteQueryDefectTracking.Infrastructure.Services;

/// <summary>
/// Default, context-free current-user service. Hosts (API/Web) register an
/// HTTP-aware implementation that supersedes this registration.
/// </summary>
public class AnonymousCurrentUserService : ICurrentUserService
{
    public string? UserId => null;
    public string? UserName => null;
    public string? Email => null;
    public bool IsAuthenticated => false;
    public IReadOnlyList<string> Roles => Array.Empty<string>();
    public bool IsInRole(string role) => false;
    public bool IsManager => false;
    public string? IpAddress => null;
    public string? DeviceInfo => null;
}

/// <summary>
/// App-time source backed by the domain clock. Delay logic never reads the
/// client clock.
/// </summary>
public class AppDateTimeService(IClock clock, string? timeZoneId = null) : IDateTimeService
{
    private readonly TimeZoneInfo _timeZone = ResolveTimeZone(timeZoneId ?? Domain.Constants.AppDefaults.TimeZoneId);

    public string TimeZoneId => _timeZone.Id;
    public TimeZoneInfo AppTimeZone => _timeZone;
    public DateTimeOffset UtcNow => clock.Now;
    public DateTimeOffset AppNow => clock.NowInBusinessTimeZone;

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}