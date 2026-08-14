using IForm.Domain.Enums;

namespace IForm.Domain.Services;

/// <summary>
/// Pure domain rules for the Site Query workflow (BRD FR-1.5, FR-2.3, FR-2.4).
/// Delay days are NEVER manually entered; they are always calculated.
/// </summary>
public static class QueryBusinessRules
{
    /// <summary>
    /// Delay days for an open query = Today - Raised Date.
    /// For a resolved query = Resolved Date - Raised Date.
    /// </summary>
    public static int CalculateDelayDays(DateTime raisedDate, DateTime? resolvedDate, DateTime today)
    {
        var end = (resolvedDate ?? today).Date;
        var start = raisedDate.Date;
        var days = (end - start).Days;
        return Math.Max(0, days);
    }

    /// <summary>
    /// Severity thresholds, configurable. Defaults from the SaaS spec:
    /// 0-7 Normal, 8-15 Watch, 16-30 Delayed, 31-45 Critical, 46+ Severe.
    /// </summary>
    public static SeverityLevel ClassifySeverity(int delayDays, DelayThresholds thresholds)
    {
        if (delayDays > thresholds.Severe) return SeverityLevel.Severe;
        if (delayDays > thresholds.Critical) return SeverityLevel.Critical;
        if (delayDays > thresholds.Delayed) return SeverityLevel.Delayed;
        if (delayDays > thresholds.Watch) return SeverityLevel.Watch;
        return SeverityLevel.Normal;
    }

    public static SeverityLevel ClassifySeverity(int delayDays) =>
        ClassifySeverity(delayDays, DelayThresholds.Default);
}

public sealed record DelayThresholds(int Watch, int Delayed, int Critical, int Severe)
{
    public static readonly DelayThresholds Default = new(7, 15, 30, 45);
}
