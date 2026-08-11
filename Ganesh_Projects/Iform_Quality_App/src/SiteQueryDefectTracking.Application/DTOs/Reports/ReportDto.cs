namespace SiteQueryDefectTracking.Application.DTOs.Reports;

public enum ReportType
{
    OpenQueries = 1,
    ResolvedQueries = 2,
    DelayReport = 3,
    IssueTypeReport = 4,
    ProjectReport = 5
}

public enum ReportFormat
{
    Excel = 1,
    Csv = 2,
    Pdf = 3
}

public class ReportRequest
{
    public ReportType Type { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Excel;
    public Guid? ProjectId { get; set; }
    public Guid? IssueTypeId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public record ReportResult(byte[] Content, string ContentType, string FileName);

public record ReportData(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);