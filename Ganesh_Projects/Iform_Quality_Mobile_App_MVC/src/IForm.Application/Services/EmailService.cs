using System.Text;
using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IEmailService
{
    Task<IReadOnlyList<EmailTemplate>> GetTemplatesAsync(CancellationToken ct = default);
    Task<EmailTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default);
    Task<EmailTemplate> SaveTemplateAsync(EmailTemplate template, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);
    Task<EmailTemplate> GetDefaultTemplateForIssueAsync(IssueType issueType, CancellationToken ct = default);
    Task<string> RenderBodyAsync(EmailTemplate template, SiteQuery query, CancellationToken ct = default);
    Task<string> RenderSubjectAsync(EmailTemplate template, SiteQuery query, CancellationToken ct = default);
    Task<EmailRecord> PreviewAsync(Guid templateId, Guid queryId, CancellationToken ct = default);
    Task<EmailRecord> SaveDraftAsync(Guid? templateId, Guid? queryId, string to, string cc, string bcc, string subject, string body, CancellationToken ct = default);
    Task<EmailRecord> SendAsync(Guid recordId, CancellationToken ct = default);
    Task<IReadOnlyList<EmailRecord>> GetHistoryAsync(Guid? queryId = null, int take = 100, CancellationToken ct = default);
}

public class EmailService : IEmailService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IEmailSender _emailSender;

    public EmailService(IApplicationDbContext db, ICurrentUser currentUser, IAuditLogger audit, IEmailSender emailSender)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _emailSender = emailSender;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    public async Task<IReadOnlyList<EmailTemplate>> GetTemplatesAsync(CancellationToken ct = default) =>
        await _db.EmailTemplates
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<EmailTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default) =>
        await _db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct);

    public async Task<EmailTemplate> SaveTemplateAsync(EmailTemplate template, CancellationToken ct = default)
    {
        if (template.Id == Guid.Empty)
        {
            template.TenantId = Tenant;
            template.CreatedBy = _currentUser.UserName;
            _db.EmailTemplates.Add(template);
        }
        else
        {
            var existing = await _db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == template.Id && x.TenantId == Tenant, ct)
                ?? throw new NotFoundException("Email template not found.");
            existing.Name = template.Name;
            existing.IssueType = template.IssueType;
            existing.Subject = template.Subject;
            existing.Body = template.Body;
            existing.IsActive = template.IsActive;
            existing.ToRecipients = template.ToRecipients;
            existing.CcRecipients = template.CcRecipients;
            existing.BccRecipients = template.BccRecipients;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserName;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Email Template Saved", nameof(EmailTemplate), template.Id.ToString(), null, template.Name, ct);
        return template;
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Email template not found.");
        template.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Email Template Deleted", nameof(EmailTemplate), id.ToString(), null, template.Name, ct);
    }

    public async Task<EmailTemplate> GetDefaultTemplateForIssueAsync(IssueType issueType, CancellationToken ct = default)
    {
        var typeName = IssueTypeName(issueType);
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(x => x.TenantId == Tenant && x.IssueType == typeName && x.IsDefault, ct)
            ?? await _db.EmailTemplates
                .FirstOrDefaultAsync(x => x.TenantId == Tenant && x.IssueType == typeName && x.IsActive, ct);

        if (template != null) return template;

        var created = new EmailTemplate
        {
            TenantId = Tenant,
            Name = $"{typeName} Template",
            IssueType = typeName,
            Subject = DefaultSubject(issueType),
            Body = DefaultBody(issueType),
            IsActive = true,
            IsDefault = true
        };
        _db.EmailTemplates.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<string> RenderBodyAsync(EmailTemplate template, SiteQuery query, CancellationToken ct = default) =>
        ReplacePlaceholders(template.Body, template, query);

    public async Task<string> RenderSubjectAsync(EmailTemplate template, SiteQuery query, CancellationToken ct = default) =>
        ReplacePlaceholders(template.Subject, template, query);

    public async Task<EmailRecord> PreviewAsync(Guid templateId, Guid queryId, CancellationToken ct = default)
    {
        var template = await GetTemplateAsync(templateId, ct) ?? throw new NotFoundException("Email template not found.");
        var query = await _db.Queries
            .Include(x => x.Project)
            .Include(x => x.RaisedByUser)
            .FirstOrDefaultAsync(x => x.Id == queryId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Query not found.");

        return new EmailRecord
        {
            TenantId = Tenant,
            TemplateName = template.Name,
            QueryId = query.Id,
            To = template.ToRecipients ?? query.Project?.Client ?? string.Empty,
            Cc = template.CcRecipients ?? string.Empty,
            Bcc = template.BccRecipients ?? string.Empty,
            Subject = await RenderSubjectAsync(template, query, ct),
            Body = await RenderBodyAsync(template, query, ct),
            IsHtml = true,
            IsDraft = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId ?? Guid.Empty
        };
    }

    public async Task<EmailRecord> SaveDraftAsync(Guid? templateId, Guid? queryId, string to, string cc, string bcc, string subject, string body, CancellationToken ct = default)
    {
        var record = new EmailRecord
        {
            TenantId = Tenant,
            TemplateName = templateId.HasValue ? (await GetTemplateAsync(templateId.Value, ct))?.Name ?? "Custom" : "Custom",
            QueryId = queryId,
            To = to ?? string.Empty,
            Cc = cc ?? string.Empty,
            Bcc = bcc ?? string.Empty,
            Subject = subject ?? string.Empty,
            Body = body ?? string.Empty,
            IsHtml = true,
            IsDraft = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId ?? Guid.Empty
        };
        _db.EmailRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Email Draft Saved", nameof(EmailRecord), record.Id.ToString(), null, subject, ct);
        return record;
    }

    public async Task<EmailRecord> SendAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await _db.EmailRecords.FirstOrDefaultAsync(x => x.Id == recordId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Email record not found.");

        record.To = string.Join(",", record.To.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(record.To))
            throw new DomainException("At least one recipient is required.");

        try
        {
            await _emailSender.SendAsync(new EmailMessage(record.To, record.Subject, record.Body, record.Cc, record.Bcc, record.IsHtml), ct);
            record.Sent = true;
            record.IsDraft = false;
            record.SentAt = DateTime.UtcNow;
            record.Error = null;
        }
        catch (Exception ex)
        {
            record.Sent = false;
            record.Error = ex.Message;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("Email Failed", nameof(EmailRecord), record.Id.ToString(), null, ex.Message, ct);
            throw;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Email Sent", nameof(EmailRecord), record.Id.ToString(), null, record.Subject, ct);
        return record;
    }

    public async Task<IReadOnlyList<EmailRecord>> GetHistoryAsync(Guid? queryId = null, int take = 100, CancellationToken ct = default)
    {
        var query = _db.EmailRecords.Where(x => x.TenantId == Tenant).AsNoTracking();
        if (queryId.HasValue) query = query.Where(x => x.QueryId == queryId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    private string ReplacePlaceholders(string text, EmailTemplate template, SiteQuery query)
    {
        var quantity = query.QuantityNos.HasValue
            ? $"{query.QuantityNos:0.##} nos" + (query.QuantitySqm.HasValue ? $" / {query.QuantitySqm:0.##} sqm" : "")
            : (query.QuantitySqm?.ToString("0.##") + " sqm");
        var dispatch = query.DispatchStatus switch
        {
            DispatchStatus.Dispatched => "Dispatched",
            DispatchStatus.Partial => "Partially Dispatched",
            _ => "Pending"
        };
        var status = query.Status switch
        {
            QueryStatus.InProgress => "In Progress",
            QueryStatus.Resolved => "Resolved",
            _ => "Pending"
        };

        var replacements = new Dictionary<string, string>
        {
            ["{IPO}"] = query.IpoNumber,
            ["{Project}"] = query.Project?.DisplayName ?? string.Empty,
            ["{IssueType}"] = IssueTypeName(query.IssueType),
            ["{Quantity}"] = quantity,
            ["{QuantityNos}"] = query.QuantityNos?.ToString("0.##") ?? "-",
            ["{QuantitySqm}"] = query.QuantitySqm?.ToString("0.##") ?? "-",
            ["{RaisedBy}"] = _currentUser.FullName ?? _currentUser.UserName ?? string.Empty,
            ["{Date}"] = query.RaisedDate.ToString("dd/MM/yyyy"),
            ["{Time}"] = query.RaisedDate.ToString("HH:mm"),
            ["{Status}"] = status,
            ["{DispatchStatus}"] = dispatch,
            ["{ProductCode}"] = query.ProductCode ?? string.Empty,
            ["{ProductName}"] = query.ProductName ?? string.Empty,
            ["{QueryNumber}"] = query.QueryNumber,
            ["{Comments}"] = query.Comments ?? string.Empty
        };

        var sb = new StringBuilder(text);
        foreach (var (key, value) in replacements)
            sb.Replace(key, value);
        return sb.ToString();
    }

    public static string IssueTypeName(IssueType issueType) => issueType switch
    {
        IssueType.Missing => "Missing",
        IssueType.ProductionMistake => "Production Mistake",
        IssueType.DesignMistake => "Design Mistake",
        IssueType.DispatchMissing => "Dispatch Missing",
        _ => issueType.ToString()
    };

    private static string DefaultSubject(IssueType issueType) =>
        $"[{{IPO}}] {{Project}} \u2013 {IssueTypeName(issueType)} Reported";

    private static string DefaultBody(IssueType issueType)
    {
        var intro = issueType switch
        {
            IssueType.Missing => "A missing item has been reported for the project below. Please review and advise on the dispatch status.",
            IssueType.ProductionMistake => "A production mistake has been identified in the material supplied for the project below. Please review and advise on the corrective action.",
            IssueType.DesignMistake => "A design mistake has been identified in the products supplied for the project below. Please review and advise on the revised drawings / rectification.",
            IssueType.DispatchMissing => "A dispatch shortfall has been identified for the project below. Please review and arrange the missing quantities.",
            _ => "A site query has been raised for the project below. Please review and advise."
        };

        return $@"
Dear Team,

{intro}

Project: {{Project}}
IPO Number: {{IPO}}
Issue Type: {IssueTypeName(issueType)}
Product Code: {{ProductCode}}
Product Name: {{ProductName}}
Quantity: {{Quantity}}
Dispatch Status: {{DispatchStatus}}
Status: {{Status}}

Comments: {{Comments}}

Raised By: {{RaisedBy}}
Date: {{Date}} {{Time}}
Query Number: {{QueryNumber}}

Kindly review and update the status of this query at the earliest.

Thanks &amp; Regards,
I-FORM Aluminium &amp; Design LLP";
    }
}
