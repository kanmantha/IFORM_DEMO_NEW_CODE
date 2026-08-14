using IForm.Application.Services;
using IForm.Domain.Entities;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "ManagerOnly")]
public class EmailController : Controller
{
    private readonly IEmailService _email;
    private readonly IQueryService _queries;

    public EmailController(IEmailService email, IQueryService queries)
    {
        _email = email;
        _queries = queries;
    }

    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        return View(await _email.GetTemplatesAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> EditTemplate(Guid? id, CancellationToken ct)
    {
        var template = id.HasValue
            ? await _email.GetTemplateAsync(id.Value, ct) ?? throw new NotFoundException("Template not found.")
            : new EmailTemplate { Name = "New Template", IsActive = true };
        return View(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(EmailTemplate template, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(template);
        try
        {
            await _email.SaveTemplateAsync(template, ct);
            TempData["Success"] = "Template saved.";
            return RedirectToAction(nameof(Templates));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        return View(template);
    }

    public async Task<IActionResult> Compose(Guid? templateId, Guid? queryId, CancellationToken ct)
    {
        ViewBag.Templates = await _email.GetTemplatesAsync(ct);
        ViewBag.Queries = (await _queries.GetRecentOpenAsync(50, ct)).Select(q => new QuerySelectOption(q.Id, q.QueryNumber));
        EmailRecord? draft = null;
        if (templateId.HasValue && queryId.HasValue && queryId.Value != Guid.Empty)
            draft = await _email.PreviewAsync(templateId.Value, queryId.Value, ct);
        return View(draft ?? new EmailRecord());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(Guid? templateId, Guid? queryId, string to, string cc, string bcc, string subject, string body, CancellationToken ct)
    {
        var record = await _email.SaveDraftAsync(templateId, queryId, to, cc, bcc, subject, body, ct);
        TempData["Success"] = "Draft saved.";
        return RedirectToAction(nameof(History));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(Guid recordId, CancellationToken ct)
    {
        try
        {
            var record = await _email.SendAsync(recordId, ct);
            TempData["Success"] = $"Email sent to {record.To}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(History));
    }

    public async Task<IActionResult> History(Guid? queryId, CancellationToken ct)
    {
        return View(await _email.GetHistoryAsync(queryId, 200, ct));
    }
}

public record QuerySelectOption(Guid Id, string QueryNumber);
