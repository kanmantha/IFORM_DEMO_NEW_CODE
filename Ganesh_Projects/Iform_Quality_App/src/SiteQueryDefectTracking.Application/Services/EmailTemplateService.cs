using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.DTOs.Email;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Application.Services;

public class EmailTemplateService(IApplicationDbContext context) : IEmailTemplateService
{
    public async Task<IReadOnlyList<EmailTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.EmailTemplates
            .AsNoTracking()
            .Include(t => t.IssueType)
            .OrderBy(t => t.Name)
            .Select(t => new EmailTemplateDto(
                t.Id, t.Name, t.Code, t.IssueTypeId, t.IssueType != null ? t.IssueType.Name : null,
                t.Subject, t.Body, t.IsActive, t.IsDefault, t.DefaultRecipients))
            .ToListAsync(ct);
    }

    public async Task<EmailTemplateDto> UpsertAsync(UpsertEmailTemplateRequest request, CancellationToken ct = default)
    {
        var template = await context.EmailTemplates
            .FirstOrDefaultAsync(t => t.Code == request.Code.Trim(), ct);

        if (template is null)
        {
            template = new EmailTemplate
            {
                Code = request.Code.Trim(),
                Name = request.Name.Trim(),
                IssueTypeId = request.IssueTypeId,
                DefaultRecipients = request.Description,
                Subject = request.SubjectTemplate,
                Body = request.BodyTemplate,
                IsActive = request.IsActive
            };
            context.EmailTemplates.Add(template);
        }
        else
        {
            template.Name = request.Name.Trim();
            template.IssueTypeId = request.IssueTypeId;
            template.DefaultRecipients = request.Description;
            template.Subject = request.SubjectTemplate;
            template.Body = request.BodyTemplate;
            template.IsActive = request.IsActive;
        }

        await context.SaveChangesAsync(ct);

        var saved = await context.EmailTemplates.Include(t => t.IssueType)
            .FirstAsync(t => t.Id == template.Id, ct);
        return new EmailTemplateDto(
            saved.Id, saved.Name, saved.Code, saved.IssueTypeId, saved.IssueType?.Name,
            saved.Subject, saved.Body, saved.IsActive, saved.IsDefault, saved.DefaultRecipients);
    }
}