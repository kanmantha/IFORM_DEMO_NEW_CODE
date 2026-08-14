using IForm.Domain.Enums;
using IForm.Domain.Services;
using Shouldly;

namespace IForm.UnitTests;

public class QueryBusinessRulesTests
{
    [Theory]
    [InlineData(0, SeverityLevel.Normal)]
    [InlineData(7, SeverityLevel.Normal)]
    [InlineData(8, SeverityLevel.Watch)]
    [InlineData(15, SeverityLevel.Watch)]
    [InlineData(16, SeverityLevel.Delayed)]
    [InlineData(30, SeverityLevel.Delayed)]
    [InlineData(31, SeverityLevel.Critical)]
    [InlineData(45, SeverityLevel.Critical)]
    [InlineData(46, SeverityLevel.Severe)]
    [InlineData(120, SeverityLevel.Severe)]
    public void ClassifySeverity_default_thresholds_hit_boundaries(int delayDays, SeverityLevel expected)
    {
        QueryBusinessRules.ClassifySeverity(delayDays).ShouldBe(expected);
    }

    [Theory]
    [InlineData(-5, SeverityLevel.Normal)]
    [InlineData(int.MaxValue, SeverityLevel.Severe)]
    public void ClassifySeverity_handles_edge_inputs(int delayDays, SeverityLevel expected)
    {
        QueryBusinessRules.ClassifySeverity(delayDays).ShouldBe(expected);
    }

    [Fact]
    public void ClassifySeverity_uses_custom_thresholds()
    {
        var custom = new DelayThresholds(Watch: 3, Delayed: 10, Critical: 20, Severe: 30);
        QueryBusinessRules.ClassifySeverity(4, custom).ShouldBe(SeverityLevel.Watch);
        QueryBusinessRules.ClassifySeverity(11, custom).ShouldBe(SeverityLevel.Delayed);
        QueryBusinessRules.ClassifySeverity(31, custom).ShouldBe(SeverityLevel.Severe);
    }

    [Fact]
    public void CalculateDelayDays_open_query_counts_from_raise_date_to_today()
    {
        var raised = new DateTime(2026, 8, 1, 10, 0, 0);
        var today = new DateTime(2026, 8, 13);
        QueryBusinessRules.CalculateDelayDays(raised, resolvedDate: null, today).ShouldBe(12);
    }

    [Fact]
    public void CalculateDelayDays_resolved_query_counts_to_resolved_date()
    {
        var raised = new DateTime(2026, 8, 1);
        var resolved = new DateTime(2026, 8, 9, 14, 30, 0);
        QueryBusinessRules.CalculateDelayDays(raised, resolved, new DateTime(2026, 8, 20)).ShouldBe(8);
    }

    [Fact]
    public void CalculateDelayDays_never_negative_for_future_raise_dates()
    {
        var raised = new DateTime(2026, 9, 1);
        QueryBusinessRules.CalculateDelayDays(raised, resolvedDate: null, new DateTime(2026, 8, 20)).ShouldBe(0);
    }
}
