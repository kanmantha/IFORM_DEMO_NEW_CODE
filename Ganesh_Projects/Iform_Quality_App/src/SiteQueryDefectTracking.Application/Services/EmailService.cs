using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SiteQueryDefectTracking.Application.DTOs.Email;
using SiteQueryDefectTracking.Application.Exceptions;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Constants;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Application.Services;

public class EmailService(
    IApplicationDbContext context,
    IEmailSender sender,
    ICurrentUserService currentUser,
    IAuditLogService auditLog,
    IOptions<EmailOptions> options) : IEmailService
{
    private static readonly string[] AvailableTokens =
    {
        "{IPO}", "{Project}", "{ProjectCode}", "{IssueType}", "{Sender}", "{QueryNo}",
        "{RaiseDate}", "{QuantityNos}", "{QuantitySqm}", "{ProductCode}", "{Description}",
        "{Link}", "{Today}"
    };

    public async Task<GeneratedEmailDto> GenerateAsync(GenerateEmailRequest request, CancellationToken ct = default)
    {
        var query = await LoadQueryAsync(request.QueryId, ct);
        var template = await ResolveTemplateAsync(request.TemplateId, query, ct);

        var senderName = currentUser.UserName ?? query.RaisedByUser?.UserName ?? string.Empty;
        var rendered = RenderTemplate(template.Subject, template.Body, query, senderName);
        var recipient = string.IsNullOrWhiteSpace(request.Recipient)
            ? template.DefaultRecipients?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty
            : request.Recipient.Trim();

        var draftId = Guid.NewGuid();
        context.EmailLogs.Add(new EmailLog
        {
            Id = draftId,
            QueryId = query.Id,
            TemplateId = template.Id,
            Recipient = recipient,
            Sender = options.Value.FromAddress,
            Subject = rendered.Subject,
            Body = rendered.Body,
            Status = EmailStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.EmailGenerated, nameof(EmailLog), draftId.ToString(),
            null, rendered.Subject, currentUser.IpAddress, currentUser.DeviceInfo), ct);

        return new GeneratedEmailDto(
            draftId, query.Id, template.Id, template.Name,
            options.Value.FromAddress, recipient, rendered.Subject, rendered.Body, AvailableTokens);
    }

    public async Task<Guid> SendAsync(SendEmailRequest request, CancellationToken ct = default)
    {
        var query = await LoadQueryAsync(request.QueryId, ct);
        var recipient = request.Recipient.Trim();
        var message = new EmailMessage(options.Value.FromAddress, new[] { recipient }, request.Subject, request.Body);

        Guid logId;
        if (sender.IsConfigured)
        {
            try
            {
                await sender.SendAsync(message, ct);
                logId = await SaveLogAsync(query.Id, request.TemplateId, recipient, message, EmailStatus.Sent, null, ct);
                await auditLog.RecordAsync(new AuditLogEntry(
                    currentUser.UserId, AuditActions.EmailSent, nameof(EmailLog), logId.ToString(),
                    null, request.Subject, currentUser.IpAddress, currentUser.DeviceInfo), ct);
            }
            catch (Exception ex)
            {
                logId = await SaveLogAsync(query.Id, request.TemplateId, recipient, message, EmailStatus.Failed, ex.Message, ct);
                await auditLog.RecordAsync(new AuditLogEntry(
                    currentUser.UserId, AuditActions.EmailFailed, nameof(EmailLog), logId.ToString(),
                    null, request.Subject, currentUser.IpAddress, currentUser.DeviceInfo), ct);
                throw;
            }
        }
        else
        {
            logId = await SaveLogAsync(query.Id, request.TemplateId, recipient, message, EmailStatus.Sent,
                "dev-mode (SMTP not configured)", ct);
            await auditLog.RecordAsync(new AuditLogEntry(
                currentUser.UserId, AuditActions.EmailSent, nameof(EmailLog), logId.ToString(),
                null, request.Subject, currentUser.IpAddress, currentUser.DeviceInfo), ct);
        }

        return logId;
    }

    public async Task<Guid> SendPreviewAsync(SendPreviewRequest request, CancellationToken ct = default)
    {
        var message = new EmailMessage(options.Value.FromAddress, new[] { request.To }, request.Subject, request.Body);
        var configured = sender.IsConfigured;

        if (configured)
        {
            try
            {
                await sender.SendAsync(message, ct);
            }
            catch (Exception ex)
            {
                throw new BusinessException($"Email failed to send: {ex.Message}");
            }
        }

        var log = new EmailLog
        {
            Recipient = request.To,
            Sender = options.Value.FromAddress,
            Subject = request.Subject,
            Body = request.Body,
            Status = configured ? EmailStatus.Sent : EmailStatus.Draft,
            SentAt = configured ? DateTimeOffset.UtcNow : null
        };
        context.EmailLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log.Id;
    }

    private async Task<Query> LoadQueryAsync(Guid queryId, CancellationToken ct)
    {
        return await context.Queries
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.RaisedByUser)
            .Include(q => q.VerifiedProductCode)
            .FirstOrDefaultAsync(q => q.Id == queryId, ct)
            ?? throw new NotFoundException("Query", queryId);
    }

    private async Task<EmailTemplate> ResolveTemplateAsync(Guid? templateId, Query query, CancellationToken ct)
    {
        if (templateId.HasValue)
        {
            var template = await context.EmailTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId.Value && t.IsActive, ct);
            if (template is null) throw new NotFoundException("Email template", templateId.Value);
            return template;
        }

        var byIssue = await context.EmailTemplates
            .Where(t => t.IsActive && t.IsDefault && t.IssueTypeId == query.IssueTypeId)
            .FirstOrDefaultAsync(ct);
        if (byIssue is not null) return byIssue;

        var fallback = await context.EmailTemplates
            .Where(t => t.IsActive && t.IsDefault)
            .FirstOrDefaultAsync(ct);
        if (fallback is null)
            throw new BusinessException("No email template is available for this issue type.");

        return fallback;
    }

    private static (string Subject, string Body) RenderTemplate(
        string subjectTemplate, string bodyTemplate, Query query, string senderName)
    {
        var replacements = new Dictionary<string, string>
        {
            ["{IPO}"] = query.IPO,
            ["{Project}"] = query.Project?.Name ?? string.Empty,
            ["{ProjectCode}"] = query.Project?.Code ?? string.Empty,
            ["{IssueType}"] = query.IssueType?.Name ?? string.Empty,
            ["{Sender}"] = senderName,
            ["{QueryNo}"] = query.QueryNo,
            ["{RaiseDate}"] = query.RaiseDate.ToString("yyyy-MM-dd HH:mm"),
            ["{QuantityNos}"] = query.QuantityNos?.ToString() ?? string.Empty,
            ["{QuantitySqm}"] = query.QuantitySqm?.ToString() ?? string.Empty,
            ["{ProductCode}"] = query.VerifiedProductCode?.Code ?? query.ProductCodeText ?? string.Empty,
            ["{Description}"] = query.Description ?? string.Empty,
            ["{Link}"] = string.Empty,
            ["{Today}"] = DateTimeOffset.Now.ToString("yyyy-MM-dd")
        };

        string Replace(string value) =>
            replacements.Aggregate(value,
                (current, pair) => current.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase));

        return (Replace(subjectTemplate), Replace(bodyTemplate));
    }

    private async Task<Guid> SaveLogAsync(
        Guid queryId, Guid? templateId, string recipient, EmailMessage message, EmailStatus status, string? error, CancellationToken ct)
    {
        var log = new EmailLog
        {
            QueryId = queryId,
            TemplateId = templateId,
            Recipient = recipient,
            Sender = options.Value.FromAddress,
            Subject = message.Subject,
            Body = message.Body,
            Status = status,
            ErrorMessage = error,
            SentAt = status == EmailStatus.Sent ? DateTimeOffset.UtcNow : null
        };
        context.EmailLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log.Id;
    }
}

public class EmailOptions
{
    public string FromAddress { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
}