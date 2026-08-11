namespace SiteQueryDefectTracking.Application.DTOs.Email;

using SiteQueryDefectTracking.Application.Common;

public record EmailTemplateDto(
    Guid Id,
    string Name,
    string Code,
    Guid? IssueTypeId,
    string? IssueTypeName,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsActive,
    bool IsDefault,
    string? Description);

public class UpsertEmailTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? IssueTypeId { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public record TemplateTokenInfo(string Token, string Description);

public class GenerateEmailRequest
{
    public Guid QueryId { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Recipient { get; set; }
}

public record GeneratedEmailDto(
    Guid DraftId,
    Guid QueryId,
    Guid TemplateId,
    string TemplateName,
    string From,
    string To,
    string Subject,
    string Body,
    IReadOnlyList<string> AvailableTokens);

public class SendEmailRequest
{
    public Guid QueryId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
}

public class SendPreviewRequest
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class EmailTemplateSearchRequest
{
    public int Page { get; set; } = Pagination.DefaultPage;
    public int PageSize { get; set; } = Pagination.DefaultPageSize;
}

public interface IEmailDtos { }