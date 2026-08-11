using SiteQueryDefectTracking.Domain.Common;
using SiteQueryDefectTracking.Domain.Enums;
using SiteQueryDefectTracking.Domain.Constants;
using SiteQueryDefectTracking.Domain.Events;

namespace SiteQueryDefectTracking.Domain.Contracts;

/// <summary>
/// Supplies the current instant and "today" in the configured business timezone
/// (Asia/Kolkata by default; configurable).
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }

    DateTime Today { get; }

    DateTimeOffset NowInBusinessTimeZone { get; }

    bool TryGetTimeZone(string timeZoneId, out TimeZoneInfo? timeZone);
}

/// <summary>
/// Domain delay calculation. DelayDays is computed server/web/domain layer, so
/// it cannot be overridden through the UI or a crafted API call.
/// </summary>
public interface IDelayCalculator
{
    int CalculateDelayDays(DateTimeOffset raiseDate, DateTimeOffset? asOf = null);

    DelaySeverity ClassifySeverity(int delayDays);
}

public static class DelaySeverityClassifier
{
    public static DelaySeverity Classify(int delayDays) => delayDays switch
    {
        < DelayThresholds.Minor => DelaySeverity.OnTime,
        < DelayThresholds.Moderate => DelaySeverity.Minor,
        < DelayThresholds.Critical => DelaySeverity.Moderate,
        _ => DelaySeverity.Critical
    };

    public static string Label(DelaySeverity severity) => severity switch
    {
        DelaySeverity.OnTime => "On Time",
        DelaySeverity.Minor => "Minor",
        DelaySeverity.Moderate => "Moderate",
        _ => "Critical"
    };
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : DomainEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : DomainEvent;
}