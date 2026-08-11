using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.DTOs.Reports;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Application.Services;

public class ReportService(IApplicationDbContext context) : IReportService
{
    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        var data = await BuildDataAsync(request, ct);

        return request.Format switch
        {
            ReportFormat.Excel => ExcelFrom(data),
            ReportFormat.Csv => CsvFrom(data),
            _ => HtmlFrom(data)
        };
    }

    private async Task<ReportData> BuildDataAsync(ReportRequest request, CancellationToken ct)
    {
        IQueryable<Query> query = context.Queries.AsNoTracking()
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.RaisedByUser)
            .Include(q => q.ResolvedByUser);

        switch (request.Type)
        {
            case ReportType.OpenQueries:
                query = query.Where(q => q.Status != QueryStatus.Resolved);
                break;
            case ReportType.ResolvedQueries:
                query = query.Where(q => q.Status == QueryStatus.Resolved);
                break;
            case ReportType.DelayReport:
                query = query.Where(q => q.DelayDays > 0);
                break;
            default:
                break;
        }

        if (request.ProjectId.HasValue) query = query.Where(q => q.ProjectId == request.ProjectId.Value);
        if (request.IssueTypeId.HasValue) query = query.Where(q => q.IssueTypeId == request.IssueTypeId.Value);
        if (request.From.HasValue) query = query.Where(q => q.RaiseDate >= request.From.Value);
        if (request.To.HasValue) query = query.Where(q => q.RaiseDate <= request.To.Value);

        var rows = await query.OrderByDescending(q => q.DelayDays).Take(5000).ToListAsync(ct);

        var columns = new List<string>
        {
            "Query No", "IPO", "Project", "Issue Type", "Status",
            "Quantity (Nos)", "Quantity (SQM)", "Product Code",
            "Raised By", "Raise Date", "Resolved Date", "Delay Days"
        };

        var rowItems = new List<IReadOnlyList<string>>();
        foreach (var q in rows)
        {
            rowItems.Add(new List<string>
            {
                q.QueryNo,
                q.IPO,
                q.Project?.Name ?? string.Empty,
                q.IssueType?.Name ?? string.Empty,
                q.Status.ToString(),
                (q.QuantityNos ?? 0).ToString(CultureInfo.InvariantCulture),
                q.QuantitySqm?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                q.VerifiedProductCode?.Code ?? q.ProductCodeText ?? string.Empty,
                q.RaisedByUser?.FullName ?? string.Empty,
                q.RaiseDate.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                q.ResolvedDate?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                q.DelayDays.ToString(CultureInfo.InvariantCulture)
            });
        }

        var title = request.Type switch
        {
            ReportType.OpenQueries => "Open Queries",
            ReportType.ResolvedQueries => "Resolved Queries",
            ReportType.DelayReport => "Delay Report",
            ReportType.IssueTypeReport => "Issue Type Report",
            ReportType.ProjectReport => "Project Report",
            _ => "Queries Report"
        };

        if (request.Type is ReportType.IssueTypeReport or ReportType.ProjectReport)
        {
            columns = new List<string> { "Name", "Open Count", "Average Delay", "Total Delay Days" };
            rowItems.Clear();
            var grouped = request.Type == ReportType.IssueTypeReport
                ? rows.GroupBy(q => q.IssueType?.Name ?? "Unknown")
                : rows.GroupBy(q => q.Project?.Name ?? "Unknown");
            foreach (var group in grouped)
            {
                rowItems.Add(new List<string>
                {
                    group.Key,
                    group.Count().ToString(),
                    group.Average(q => q.DelayDays).ToString("0.0", CultureInfo.InvariantCulture),
                    group.Sum(q => q.DelayDays).ToString()
                });
            }
        }

        return new ReportData(title, columns, rowItems);
    }

    private static ReportResult ExcelFrom(ReportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(data.Title);
        for (var c = 0; c < data.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = data.Columns[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < data.Rows.Count; r++)
        {
            for (var c = 0; c < data.Rows[r].Count; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = data.Rows[r][c];
            }
        }

        sheet.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return new ReportResult(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{data.Title}.xlsx");
    }

    private static ReportResult CsvFrom(ReportData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", data.Columns.Select(CsvEscape)));
        foreach (var row in data.Rows)
            sb.AppendLine(string.Join(",", row.Select(CsvEscape)));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new ReportResult(bytes, "text/csv", $"{data.Title}.csv");

        static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }

    private static ReportResult HtmlFrom(ReportData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>").Append(data.Title).AppendLine("</title></head><body>");
        sb.Append("<h1>").Append(data.Title).AppendLine("</h1><table border=\"1\" cellpadding=\"4\"><thead><tr>");
        foreach (var column in data.Columns) sb.Append("<th>").Append(System.Net.WebUtility.HtmlEncode(column)).Append("</th>");
        sb.AppendLine("</tr></thead><tbody>");
        foreach (var row in data.Rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row) sb.Append("<td>").Append(System.Net.WebUtility.HtmlEncode(cell)).Append("</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table></body></html>");
        return new ReportResult(Encoding.UTF8.GetBytes(sb.ToString()), "text/html", $"{data.Title}.pdf.html");
    }
}