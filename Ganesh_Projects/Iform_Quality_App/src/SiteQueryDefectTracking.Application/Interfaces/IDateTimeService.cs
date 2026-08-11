namespace SiteQueryDefectTracking.Application.Interfaces;

/// <summary>
/// Date/time source. All delay calculations use business-timezone "now" supplied
/// by this service so timezone handling is configurable and never client-side.
/// </summary>
public interface IDateTimeService
{
    string TimeZoneId { get; }
    TimeZoneInfo AppTimeZone { get; }
    DateTimeOffset UtcNow { get; }

    /// <summary>Current instant expressed in the configured business timezone.</summary>
    DateTimeOffset AppNow { get; }
}