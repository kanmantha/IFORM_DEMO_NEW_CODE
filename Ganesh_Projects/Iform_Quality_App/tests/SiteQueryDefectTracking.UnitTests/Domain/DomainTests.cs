using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Services;
using SiteQueryDefectTracking.Domain.Contracts;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;
using AuditActions = SiteQueryDefectTracking.Domain.Constants.AuditActions;
using IssueTypeCodes = SiteQueryDefectTracking.Domain.Constants.IssueTypeCodes;

namespace SiteQueryDefectTracking.UnitTests.Domain;

public class DelaySeverityClassifierTests
{
    [Theory]
    [InlineData(0, DelaySeverity.OnTime)]
    [InlineData(1, DelaySeverity.OnTime)]
    [InlineData(2, DelaySeverity.OnTime)]
    public void BelowMinor_IsOnTime(int days, DelaySeverity expected)
    {
        Assert.Equal(expected, DelaySeverityClassifier.Classify(days));
    }

    [Theory]
    [InlineData(3, DelaySeverity.Minor)]
    [InlineData(6, DelaySeverity.Minor)]
    public void AtOrAboveMinor_IsMinor(int days, DelaySeverity expected)
    {
        Assert.Equal(expected, DelaySeverityClassifier.Classify(days));
    }

    [Theory]
    [InlineData(7, DelaySeverity.Moderate)]
    [InlineData(13, DelaySeverity.Moderate)]
    public void AtOrAboveModerate_IsModerate(int days, DelaySeverity expected)
    {
        Assert.Equal(expected, DelaySeverityClassifier.Classify(days));
    }

    [Theory]
    [InlineData(14, DelaySeverity.Critical)]
    [InlineData(30, DelaySeverity.Critical)]
    public void AtOrAboveCritical_IsCritical(int days, DelaySeverity expected)
    {
        Assert.Equal(expected, DelaySeverityClassifier.Classify(days));
    }

    [Fact]
    public void Labels_AreExpected()
    {
        Assert.Equal("On Time", DelaySeverityClassifier.Label(DelaySeverity.OnTime));
        Assert.Equal("Minor", DelaySeverityClassifier.Label(DelaySeverity.Minor));
        Assert.Equal("Moderate", DelaySeverityClassifier.Label(DelaySeverity.Moderate));
        Assert.Equal("Critical", DelaySeverityClassifier.Label(DelaySeverity.Critical));
    }
}

public class QueryMappersTests
{
    private static Query CreateQuery(QueryStatus status, int delayDays = 0) => new()
    {
        Id = Guid.NewGuid(),
        QueryNo = "SQ-202608-0001",
        IPO = "IPO-001",
        ProjectId = Guid.NewGuid(),
        Project = new Project { Id = Guid.NewGuid(), Name = "Proj A" },
        IssueTypeId = Guid.NewGuid(),
        IssueType = new IssueType { Id = Guid.NewGuid(), Name = "Missing", Code = IssueTypeCodes.Missing },
        Status = status,
        QuantityNos = 5,
        QuantitySqm = 10.5m,
        VerifiedProductCodeId = null,
        ProductCodeText = "PC-001",
        DispatchStatus = DispatchStatus.NotDispatched,
        RaisedByUserId = "user-1",
        RaisedByUser = new User { Id = "user-1", UserName = "se", FirstName = "Site", LastName = "Engineer" },
        RaiseDate = DateTimeOffset.UtcNow.AddDays(-delayDays),
        DelayDays = delayDays,
        Description = "Test description"
    };

    [Fact]
    public void ToSummary_Maps_Fields()
    {
        var q = CreateQuery(QueryStatus.InProgress, delayDays: 4);
        var dto = QueryMappers.ToSummary(q);

        Assert.Equal(q.Id, dto.Id);
        Assert.Equal("IPO-001", dto.IPO);
        Assert.Equal("Proj A", dto.ProjectName);
        Assert.Equal("Missing", dto.IssueTypeName);
        Assert.Equal(QueryStatus.InProgress, dto.Status);
        Assert.Equal("PC-001", dto.ProductCode);
        Assert.Equal(5, dto.QuantityNos);
        Assert.Equal(10.5m, dto.QuantitySqm);
        Assert.Equal("Site Engineer", dto.RaisedByName);
        Assert.Equal(4, dto.DelayDays);
    }

    [Fact]
    public void ToSummary_ProductCode_FallsBackToVerifiedProductCode()
    {
        var q = CreateQuery(QueryStatus.Pending);
        q.ProductCodeText = null;
        q.VerifiedProductCodeId = Guid.NewGuid();
        q.VerifiedProductCode = new ProductCode { Id = q.VerifiedProductCodeId.Value, Code = "VERIFIED-001" };

        var dto = QueryMappers.ToSummary(q);
        Assert.Equal("VERIFIED-001", dto.ProductCode);
    }

    [Fact]
    public void SlaBreached_True_ForOpenOverdueQuery()
    {
        var q = CreateQuery(QueryStatus.InProgress, delayDays: 3);
        var dto = QueryMappers.ToSummary(q);
        Assert.True(dto.IsSlaBreached);
    }

    [Fact]
    public void SlaBreached_False_ForResolvedOverdueQuery()
    {
        var q = CreateQuery(QueryStatus.Resolved, delayDays: 3);
        var dto = QueryMappers.ToSummary(q);
        Assert.False(dto.IsSlaBreached);
    }

    [Fact]
    public void SlaBreached_False_WhenNotOverdue()
    {
        var q = CreateQuery(QueryStatus.Pending, delayDays: 0);
        var dto = QueryMappers.ToSummary(q);
        Assert.False(dto.IsSlaBreached);
    }

    [Fact]
    public void ToComment_Maps_Fields()
    {
        var comment = new QueryComment
        {
            Id = Guid.NewGuid(),
            QueryId = Guid.NewGuid(),
            UserId = "user-1",
            User = new User { Id = "user-1", FirstName = "John" },
            CommentText = "Hello",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var dto = QueryMappers.ToComment(comment);
        Assert.Equal("John", dto.UserName);
        Assert.Equal("Hello", dto.CommentText);
    }

    [Fact]
    public void ToStatusHistory_Maps_FromStatus()
    {
        var history = new QueryStatusHistory
        {
            QueryId = Guid.NewGuid(),
            FromStatus = QueryStatus.Pending,
            ToStatus = QueryStatus.Resolved,
            ChangedByUserId = "u1",
            ChangedAt = DateTimeOffset.UtcNow
        };
        var dto = QueryMappers.ToStatusHistory(history);
        Assert.Equal(QueryStatus.Pending, dto.FromStatus);
        Assert.Equal(QueryStatus.Resolved, dto.ToStatus);
    }
}

public class AuditLogMapperTests
{
    [Fact]
    public void ToAudit_Maps_Fields()
    {
        var a = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            Username = "manager",
            Action = AuditActions.Login,
            EntityName = "User",
            EntityId = "u1",
            Timestamp = DateTimeOffset.UtcNow
        };
        var dto = QueryMappers.ToAudit(a);
        Assert.Equal("manager", dto.UserName);
        Assert.Equal(AuditActions.Login, dto.Action);
    }
}