using System.Text.Json.Serialization;

namespace SiteQueryDefectTracking.Mobile.Models;

public sealed class ApiResponse<T>
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public sealed class PagedResult<T>
{
    [JsonPropertyName("items")] public List<T> Items { get; set; } = new();
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
    [JsonPropertyName("totalPages")] public int TotalPages { get; set; }
}

public enum QueryStatus { Pending = 1, InProgress = 2, Resolved = 3 }
public enum DispatchStatus { NotDispatched = 1, PartiallyDispatched = 2, Dispatched = 3 }
public enum EmailLogStatus { Draft = 1, Generated = 2, Sent = 3, Failed = 4 }
public enum NotificationType { Info = 1, QueryCreated = 2, QueryStatusChanged = 3, CommentAdded = 4, QueryResolved = 5, CriticalDelay = 6 }

public sealed class TokenResponse
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("expiresInSeconds")] public int ExpiresInSeconds { get; set; }
    [JsonPropertyName("tokenType")] public string TokenType { get; set; } = "Bearer";
}

public sealed class CurrentUser
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("fullName")] public string FullName { get; set; } = string.Empty;
    [JsonPropertyName("mobileNumber")] public string? MobileNumber { get; set; }
    [JsonPropertyName("roles")] public List<string> Roles { get; set; } = new();
}

public sealed class LookupItem
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; } = true;
}

public sealed class EnumOption
{
    [JsonPropertyName("value")] public int Value { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class CommentDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("queryId")] public Guid QueryId { get; set; }
    [JsonPropertyName("userId")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
    [JsonPropertyName("commentText")] public string CommentText { get; set; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AttachmentDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("queryId")] public Guid QueryId { get; set; }
    [JsonPropertyName("originalFileName")] public string OriginalFileName { get; set; } = string.Empty;
    [JsonPropertyName("contentType")] public string ContentType { get; set; } = string.Empty;
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "Photo";
    [JsonPropertyName("uploadedAt")] public DateTimeOffset UploadedAt { get; set; }
    [JsonPropertyName("uploadedBy")] public string UploadedBy { get; set; } = string.Empty;
    [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }
}

public sealed class QuerySummaryDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("ipo")] public string IPO { get; set; } = string.Empty;
    [JsonPropertyName("projectId")] public Guid ProjectId { get; set; }
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = string.Empty;
    [JsonPropertyName("issueTypeId")] public Guid IssueTypeId { get; set; }
    [JsonPropertyName("issueTypeName")] public string IssueTypeName { get; set; } = string.Empty;
    [JsonPropertyName("issueTypeCode")] public string? IssueTypeCode { get; set; }
    [JsonPropertyName("status")] public QueryStatus Status { get; set; }
    [JsonPropertyName("quantityNos")] public int QuantityNos { get; set; }
    [JsonPropertyName("quantitySqm")] public decimal? QuantitySqm { get; set; }
    [JsonPropertyName("verifiedProductCodeId")] public Guid? VerifiedProductCodeId { get; set; }
    [JsonPropertyName("productCode")] public string? ProductCode { get; set; }
    [JsonPropertyName("dispatchStatus")] public string? DispatchStatus { get; set; }
    [JsonPropertyName("raisedByUserId")] public string RaisedByUserId { get; set; } = string.Empty;
    [JsonPropertyName("raisedByName")] public string RaisedByName { get; set; } = string.Empty;
    [JsonPropertyName("resolvedByUserId")] public string? ResolvedByUserId { get; set; }
    [JsonPropertyName("resolvedByName")] public string? ResolvedByName { get; set; }
    [JsonPropertyName("raiseDate")] public DateTimeOffset RaiseDate { get; set; }
    [JsonPropertyName("resolvedDate")] public DateTimeOffset? ResolvedDate { get; set; }
    [JsonPropertyName("delayDays")] public int DelayDays { get; set; }
    [JsonPropertyName("slabTarget")] public string? SlabTarget { get; set; }
    [JsonPropertyName("slabCompleted")] public string? SlabCompleted { get; set; }
    [JsonPropertyName("slabDelayDays")] public int? SlabDelayDays { get; set; }
    [JsonPropertyName("attachmentCount")] public int AttachmentCount { get; set; }
    [JsonPropertyName("previewDescription")] public string? PreviewDescription { get; set; }
    [JsonPropertyName("isSlaBreached")] public bool IsSlaBreached { get; set; }
    [JsonPropertyName("isPublic")] public bool IsPublic { get; set; }

    public string StatusLabel => Status switch
    {
        QueryStatus.Pending => "Pending",
        QueryStatus.InProgress => "In Progress",
        _ => "Resolved"
    };

    public Color StatusBrush => Status switch
    {
        QueryStatus.Pending => Color.FromArgb("#E67E22"),
        QueryStatus.InProgress => Color.FromArgb("#2980B9"),
        _ => Color.FromArgb("#27AE60")
    };

    public Color DelayBrush => IsSlaBreached ? Colors.Red : Colors.Gray;

    public string SummaryLine => $"IPO {IPO} • {IssueTypeName} • {QuantityNos} nos";
}

public sealed class QueryDetailDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("ipo")] public string IPO { get; set; } = string.Empty;
    [JsonPropertyName("projectId")] public Guid ProjectId { get; set; }
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = string.Empty;
    [JsonPropertyName("issueTypeId")] public Guid IssueTypeId { get; set; }
    [JsonPropertyName("issueTypeName")] public string IssueTypeName { get; set; } = string.Empty;
    [JsonPropertyName("issueTypeCode")] public string? IssueTypeCode { get; set; }
    [JsonPropertyName("status")] public QueryStatus Status { get; set; }
    [JsonPropertyName("quantityNos")] public int QuantityNos { get; set; }
    [JsonPropertyName("quantitySqm")] public decimal? QuantitySqm { get; set; }
    [JsonPropertyName("verifiedProductCodeId")] public Guid? VerifiedProductCodeId { get; set; }
    [JsonPropertyName("productCode")] public string? ProductCode { get; set; }
    [JsonPropertyName("dispatchStatus")] public string? DispatchStatus { get; set; }
    [JsonPropertyName("raisedByUserId")] public string RaisedByUserId { get; set; } = string.Empty;
    [JsonPropertyName("raisedByName")] public string RaisedByName { get; set; } = string.Empty;
    [JsonPropertyName("resolvedByUserId")] public string? ResolvedByUserId { get; set; }
    [JsonPropertyName("resolvedByName")] public string? ResolvedByName { get; set; }
    [JsonPropertyName("raiseDate")] public DateTimeOffset RaiseDate { get; set; }
    [JsonPropertyName("resolvedDate")] public DateTimeOffset? ResolvedDate { get; set; }
    [JsonPropertyName("delayDays")] public int DelayDays { get; set; }
    [JsonPropertyName("slabTarget")] public string? SlabTarget { get; set; }
    [JsonPropertyName("slabCompleted")] public string? SlabCompleted { get; set; }
    [JsonPropertyName("slabDelayDays")] public int? SlabDelayDays { get; set; }
    [JsonPropertyName("attachmentCount")] public int AttachmentCount { get; set; }
    [JsonPropertyName("previewDescription")] public string? PreviewDescription { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("slabId")] public Guid? SlabId { get; set; }
    [JsonPropertyName("isSlaBreached")] public bool IsSlaBreached { get; set; }
    [JsonPropertyName("comments")] public List<CommentDto> Comments { get; set; } = new();
    [JsonPropertyName("attachments")] public List<AttachmentDto> Attachments { get; set; } = new();
    [JsonPropertyName("emails")] public List<EmailLogDto> Emails { get; set; } = new();
}

public sealed class EmailLogDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("queryId")] public Guid? QueryId { get; set; }
    [JsonPropertyName("templateId")] public Guid? TemplateId { get; set; }
    [JsonPropertyName("templateName")] public string? TemplateName { get; set; }
    [JsonPropertyName("recipient")] public string Recipient { get; set; } = string.Empty;
    [JsonPropertyName("sender")] public string Sender { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("status")] public EmailLogStatus Status { get; set; }
    [JsonPropertyName("sentAt")] public DateTimeOffset? SentAt { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }
}

public sealed class ProductSummaryDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("unit")] public string? Unit { get; set; }
    [JsonPropertyName("barcode")] public string? Barcode { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("lastImportedAt")] public DateTimeOffset? LastImportedAt { get; set; }
    [JsonPropertyName("projectMappingCount")] public int ProjectMappingCount { get; set; }
}

public sealed class ProductSpecificationDto
{
    [JsonPropertyName("attributeName")] public string AttributeName { get; set; } = string.Empty;
    [JsonPropertyName("attributeValue")] public string AttributeValue { get; set; } = string.Empty;
}

public sealed class ProductProjectMappingDto
{
    [JsonPropertyName("projectId")] public Guid ProjectId { get; set; }
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = string.Empty;
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
}

public sealed class ProductDetailDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("unit")] public string? Unit { get; set; }
    [JsonPropertyName("barcode")] public string? Barcode { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("lastImportedAt")] public DateTimeOffset? LastImportedAt { get; set; }
    [JsonPropertyName("specifications")] public List<ProductSpecificationDto> Specifications { get; set; } = new();
    [JsonPropertyName("projectMappings")] public List<ProductProjectMappingDto> ProjectMappings { get; set; } = new();
}

public sealed class EmailTemplateDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("issueTypeId")] public Guid? IssueTypeId { get; set; }
    [JsonPropertyName("issueTypeName")] public string? IssueTypeName { get; set; }
    [JsonPropertyName("subjectTemplate")] public string SubjectTemplate { get; set; } = string.Empty;
    [JsonPropertyName("bodyTemplate")] public string BodyTemplate { get; set; } = string.Empty;
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("isDefault")] public bool IsDefault { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public sealed class GeneratedEmailDto
{
    [JsonPropertyName("draftId")] public Guid DraftId { get; set; }
    [JsonPropertyName("queryId")] public Guid QueryId { get; set; }
    [JsonPropertyName("templateId")] public Guid TemplateId { get; set; }
    [JsonPropertyName("templateName")] public string TemplateName { get; set; } = string.Empty;
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
    [JsonPropertyName("availableTokens")] public List<string> AvailableTokens { get; set; } = new();
}

public sealed class DashboardSummaryDto
{
    [JsonPropertyName("totalOpenQueries")] public int TotalOpenQueries { get; set; }
    [JsonPropertyName("pending")] public int Pending { get; set; }
    [JsonPropertyName("inProgress")] public int InProgress { get; set; }
    [JsonPropertyName("resolvedTotal")] public int ResolvedTotal { get; set; }
    [JsonPropertyName("resolvedToday")] public int ResolvedToday { get; set; }
    [JsonPropertyName("criticalDelays")] public int CriticalDelays { get; set; }
    [JsonPropertyName("averageDelay")] public double AverageDelay { get; set; }
    [JsonPropertyName("maxDelay")] public int MaxDelay { get; set; }
    [JsonPropertyName("totalQueries")] public int TotalQueries { get; set; }
}

public sealed class IssueBreakdownDto
{
    [JsonPropertyName("issueTypeId")] public Guid IssueTypeId { get; set; }
    [JsonPropertyName("issueTypeName")] public string IssueTypeName { get; set; } = string.Empty;
    [JsonPropertyName("openCount")] public int OpenCount { get; set; }
    [JsonPropertyName("totalDelayDays")] public int? TotalDelayDays { get; set; }
}

public sealed class ProjectBreakdownDto
{
    [JsonPropertyName("projectId")] public Guid ProjectId { get; set; }
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = string.Empty;
    [JsonPropertyName("openCount")] public int OpenCount { get; set; }
    [JsonPropertyName("averageDelay")] public double AverageDelay { get; set; }
    [JsonPropertyName("totalOpenDelayDays")] public int TotalOpenDelayDays { get; set; }
}

public sealed class StatusBreakdownDto
{
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("count")] public int Count { get; set; }
}

public sealed class DashboardSnapshotDto
{
    [JsonPropertyName("summary")] public DashboardSummaryDto? Summary { get; set; }
    [JsonPropertyName("issues")] public List<IssueBreakdownDto> Issues { get; set; } = new();
    [JsonPropertyName("projects")] public List<ProjectBreakdownDto> Projects { get; set; } = new();
    [JsonPropertyName("statusDistribution")] public List<StatusBreakdownDto> StatusDistribution { get; set; } = new();
    [JsonPropertyName("openQueries")] public List<QuerySummaryDto> OpenQueries { get; set; } = new();
}

public sealed class CreateQueryPayload
{
    [JsonPropertyName("projectId")] public Guid ProjectId { get; set; }
    [JsonPropertyName("issueTypeId")] public Guid IssueTypeId { get; set; }
    [JsonPropertyName("ipo")] public string IPO { get; set; } = string.Empty;
    [JsonPropertyName("quantityNos")] public int? QuantityNos { get; set; }
    [JsonPropertyName("quantitySqm")] public decimal? QuantitySqm { get; set; }
    [JsonPropertyName("verifiedProductCodeId")] public Guid? VerifiedProductCodeId { get; set; }
    [JsonPropertyName("productCodeText")] public string? ProductCodeText { get; set; }
    [JsonPropertyName("dispatchStatus")] public string? DispatchStatus { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("slabTarget")] public string? SlabTarget { get; set; }
    [JsonPropertyName("slabCompleted")] public string? SlabCompleted { get; set; }
    [JsonPropertyName("slabDelayDays")] public int? SlabDelayDays { get; set; }
}

public sealed class QuerySearchPayload
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; } = "DelayDays";
    public string? SortDirection { get; set; } = "desc";
    public string? IPO { get; set; }
    public string? Keyword { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? IssueTypeId { get; set; }
    public QueryStatus? Status { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public bool? MineOnly { get; set; }
}