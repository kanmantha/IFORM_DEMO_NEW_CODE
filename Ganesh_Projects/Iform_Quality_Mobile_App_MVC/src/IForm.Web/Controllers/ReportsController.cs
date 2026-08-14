using System.Data;
using ClosedXML.Excel;
using IForm.Application.DTOs;
using IForm.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    [Authorize(Policy = "ManagerOnly")]
    public IActionResult Index() => View();

    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> QueryReport(string? term, Guid? projectId, CancellationToken ct)
    {
        var rows = await _reports.BuildQueryReportAsync(new QuerySearchRequest(SearchTerm: term, ProjectId: projectId), ct);
        return View(rows);
    }

    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> DelayReport(CancellationToken ct) => View(await _reports.BuildDelayReportAsync(ct));

    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> EngineerReport(CancellationToken ct) => View(await _reports.BuildEngineerReportAsync(ct));

    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> ProductIssueReport(CancellationToken ct) => View(await _reports.BuildProductIssueReportAsync(ct));

    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> EotReport(CancellationToken ct) => View(await _reports.BuildEotReportAsync(ct));

    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> UsageReport(CancellationToken ct) => View(await _reports.BuildUsageReportAsync(ct));

    [Authorize(Policy = "ManagerOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(string report, CancellationToken ct)
    {
        IReadOnlyList<ReportRow> rows = report switch
        {
            "queries" => await _reports.BuildQueryReportAsync(new QuerySearchRequest(), ct),
            "delays" => await _reports.BuildDelayReportAsync(ct),
            "engineers" => await _reports.BuildEngineerReportAsync(ct),
            "products" => await _reports.BuildProductIssueReportAsync(ct),
            "eots" => await _reports.BuildEotReportAsync(ct),
            _ => throw new InvalidOperationException("Unknown report.")
        };

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(report);
        if (rows.Count == 0)
        {
            sheet.Cell(1, 1).Value = "No data";
        }
        else
        {
            var headers = rows[0].Values.Keys.ToList();
            for (int c = 0; c < headers.Count; c++)
            {
                sheet.Cell(1, c + 1).Value = headers[c];
                sheet.Cell(1, c + 1).Style.Font.Bold = true;
            }

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int c = 0; c < headers.Count; c++)
                {
                    var value = row.Values.TryGetValue(headers[c], out var v) ? v : null;
                    if (value == null) continue;
                    var cell = sheet.Cell(r + 2, c + 1);
                    cell.Value = value switch
                    {
                        int i => i,
                        long l => l,
                        decimal d => d,
                        double dbl => dbl,
                        bool b => b,
                        _ => value.ToString()
                    };
                }
            }
            sheet.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{report}_report_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
    }
}
