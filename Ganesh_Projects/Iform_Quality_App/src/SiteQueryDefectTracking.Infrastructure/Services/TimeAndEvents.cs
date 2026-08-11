using System.Collections.Concurrent;
using SiteQueryDefectTracking.Domain.Common;
using SiteQueryDefectTracking.Domain.Contracts;
using SiteQueryDefectTracking.Domain.Enums;
using SiteQueryDefectTracking.Domain.Events;

namespace SiteQueryDefectTracking.Infrastructure.Services;

/// <summary>
/// Supplies the current instant and business-timezone date. The timezone id is
/// configurable (defaults to Asia/Kolkata and never depends on client clocks).
/// </summary>
public class SystemClock : IClock
{
    private readonly string _timeZoneId;
    private readonly TimeZoneInfo? _timeZone;

    public SystemClock(string timeZoneId = Domain.Constants.AppDefaults.TimeZoneId)
    {
        _timeZoneId = timeZoneId;
        _timeZone = ResolveTimeZone(timeZoneId);
    }

    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EffectiveTimeZone).Date;

    public DateTimeOffset NowInBusinessTimeZone =>
        new(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EffectiveTimeZone),
            EffectiveTimeZone.GetUtcOffset(DateTime.UtcNow));

    public bool TryGetTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            var resolved = ResolveTimeZone(timeZoneId);
            if (resolved is not null)
            {
                timeZone = resolved;
                return true;
            }
        }
        catch
        {
            // fall through
        }
        timeZone = TimeZoneInfo.Utc;
        return false;
    }

    private TimeZoneInfo EffectiveTimeZone => _timeZone ?? TimeZoneInfo.Utc;

    private static TimeZoneInfo? ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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

/// <summary>
/// Delay is computed at the application/domain layer — never on the client.
/// Business definition: Delay = current date - raise date, in whole business
/// days. Resolved queries preserve their historical delay against the resolved
/// date.
/// </summary>
public class DelayCalculator(IClock clock) : IDelayCalculator
{
    public int CalculateDelayDays(DateTimeOffset raiseDate, DateTimeOffset? asOf = null)
    {
        var end = asOf ?? clock.NowInBusinessTimeZone;
        var span = end.Date - raiseDate.ToOffset(end.Offset).Date;
        return span.TotalDays < 0 ? 0 : (int)Math.Floor(span.TotalDays);
    }

    public DelaySeverity ClassifySeverity(int delayDays) =>
        DelaySeverityClassifier.Classify(delayDays);
}

/// <summary>
/// Simple in-memory domain event bus used for internal event fan-out
/// (used by the API SignalR bridge and the Blazor dashboard).
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : DomainEvent
    {
        List<Delegate>? handlers;
        lock (_lock)
        {
            _handlers.TryGetValue(typeof(TEvent), out handlers);
        }

        if (handlers is null) return Task.CompletedTask;

        var tasks = handlers
            .Cast<Func<TEvent, CancellationToken, Task>>()
            .Select(h => h(eventData, cancellationToken))
            .ToArray();
        return Task.WhenAll(tasks);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : DomainEvent
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(TEvent)] = list;
            }
            list.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    private sealed record Subscription(Action Unsubscribe) : IDisposable
    {
        public void Dispose() => Unsubscribe();
    }
}