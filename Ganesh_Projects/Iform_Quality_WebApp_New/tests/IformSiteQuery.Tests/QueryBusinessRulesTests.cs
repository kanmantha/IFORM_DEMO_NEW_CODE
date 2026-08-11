using IformSiteQuery.Domain.Enums;
using IformSiteQuery.Domain.Services;

namespace IformSiteQuery.Tests;

public class QueryBusinessRulesTests
{
    [Theory]
    [InlineData(2026, 1, "QRY-2026-0001")]
    [InlineData(2026, 14, "QRY-2026-0014")]
    [InlineData(2026, 9999, "QRY-2026-9999")]
    [InlineData(2025, 42, "QRY-2025-0042")]
    public void NextQueryNumber_FormatsSequentialNumber(int year, int seq, string expected)
    {
        Assert.Equal(expected, QueryBusinessRules.NextQueryNumber(year, seq));
    }

    [Theory]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.SiteEngineer, false)]
    public void CanResolve_AllowsOnlyManager(UserRole role, bool expected)
    {
        Assert.Equal(expected, QueryBusinessRules.CanResolve(role));
    }

    [Theory]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.SiteEngineer, false)]
    public void CanSendEmail_AllowsOnlyManager(UserRole role, bool expected)
    {
        Assert.Equal(expected, QueryBusinessRules.CanSendEmail(role));
    }

    [Theory]
    [InlineData(QueryStatus.Pending, true)]
    [InlineData(QueryStatus.InProgress, true)]
    [InlineData(QueryStatus.Resolved, false)]
    public void CanTransitionToResolved_OpenQueriesOnly(QueryStatus status, bool expected)
    {
        Assert.Equal(expected, QueryBusinessRules.CanTransitionToResolved(status));
    }

    [Fact]
    public void CalculateDelayDays_OpenQuery_CountsFromRaiseDateToToday()
    {
        var raised = new DateTime(2026, 8, 1, 10, 0, 0);
        var now = new DateTime(2026, 8, 11, 9, 0, 0);
        Assert.Equal(10, QueryBusinessRules.CalculateDelayDays(raised, QueryStatus.Pending, null, now));
    }

    [Fact]
    public void CalculateDelayDays_ResolvedQuery_CountsToResolutionDate()
    {
        var raised = new DateTime(2026, 7, 20, 9, 0, 0);
        var resolved = new DateTime(2026, 8, 2, 18, 30, 0);
        var now = new DateTime(2026, 8, 20, 0, 0, 0);
        Assert.Equal(13, QueryBusinessRules.CalculateDelayDays(raised, QueryStatus.Resolved, resolved, now));
    }

    [Fact]
    public void CalculateDelayDays_ResolvedSameDay_IsZero()
    {
        var raised = new DateTime(2026, 8, 11, 8, 0, 0);
        var resolved = new DateTime(2026, 8, 11, 16, 0, 0);
        Assert.Equal(0, QueryBusinessRules.CalculateDelayDays(raised, QueryStatus.Resolved, resolved, raised));
    }

    [Fact]
    public void CalculateDelayDays_NeverNegative()
    {
        var raised = new DateTime(2026, 8, 11);
        var now = new DateTime(2026, 8, 10);
        Assert.Equal(0, QueryBusinessRules.CalculateDelayDays(raised, QueryStatus.Pending, null, now));
    }

    [Theory]
    [InlineData(null, "2026-08-11", 0)]
    [InlineData("2026-08-11", null, 0)]
    [InlineData("2026-08-11", "2026-08-11", 0)]
    [InlineData("2026-08-05", "2026-08-11", 6)]
    [InlineData("2026-08-15", "2026-08-11", 0)]
    public void CalculateSlabDelayDays_VariousTargetAndCompletion(string? target, string? completed, int expected)
    {
        DateTime? t = target is null ? null : DateTime.Parse(target);
        DateTime? c = completed is null ? null : DateTime.Parse(completed);
        Assert.Equal(expected, QueryBusinessRules.CalculateSlabDelayDays(t, c));
    }
}
