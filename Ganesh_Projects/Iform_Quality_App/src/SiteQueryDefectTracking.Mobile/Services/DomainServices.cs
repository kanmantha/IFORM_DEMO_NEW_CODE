using SiteQueryDefectTracking.Mobile.Models;

namespace SiteQueryDefectTracking.Mobile.Services;

public sealed class ProjectService(ApiClient api)
{
    public async Task<List<LookupItem>> GetActiveAsync(CancellationToken ct = default)
        => await api.GetAsync<List<LookupItem>>("api/projects/active", ct) ?? new List<LookupItem>();
}

public sealed class ReferenceService(ApiClient api)
{
    public async Task<List<LookupItem>> GetIssueTypesAsync(CancellationToken ct = default)
        => await api.GetAsync<List<LookupItem>>("api/reference/issue-types", ct) ?? new List<LookupItem>();

    public async Task<List<EnumOption>> GetDispatchStatusesAsync(CancellationToken ct = default)
        => await api.GetAsync<List<EnumOption>>("api/reference/dispatch-statuses", ct) ?? new List<EnumOption>();
}

public sealed class QueryService(ApiClient api)
{
    public async Task<Guid> CreateAsync(CreateQueryPayload payload, CancellationToken ct = default)
    {
        Guid? id = await api.PostAsync<Guid>("api/queries", payload, ct);
        return id ?? Guid.Empty;
    }

    public Task<QueryDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
        => api.GetAsync<QueryDetailDto>($"api/queries/{id}", ct);

    public async Task<PagedResult<QuerySummaryDto>> SearchAsync(QuerySearchPayload payload, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(payload);
        return await api.GetAsync<PagedResult<QuerySummaryDto>>($"api/queries?{query}", ct)
            ?? new PagedResult<QuerySummaryDto>();
    }

    public Task<QuerySummaryDto?> ChangeStatusAsync(Guid id, QueryStatus status, string? reason, CancellationToken ct = default)
        => api.PutAsync<QuerySummaryDto>($"api/queries/{id}/status", new { status = status.ToString(), reason }, ct);

    public async Task<Guid> ResolveAsync(Guid id, string? resolutionNote, CancellationToken ct = default)
    {
        Guid? result = await api.PutAsync<Guid>($"api/queries/{id}/resolve", new { resolutionNote }, ct);
        return result ?? Guid.Empty;
    }

    public Task<CommentDto?> AddCommentAsync(Guid id, string text, CancellationToken ct = default)
        => api.PostAsync<CommentDto>($"api/queries/{id}/comments", new { commentText = text }, ct);

    public Task<AttachmentDto?> UploadPhotoAsync(Guid id, string filePath, string fileName, string contentType, CancellationToken ct = default)
        => api.UploadAsync<AttachmentDto>($"api/queries/{id}/attachments", filePath, fileName, contentType, ct);

    public async Task<string?> GetPhotoUrlAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        try
        {
            var bytes = await api.DownloadAsync($"api/queries/{id}/attachments/{attachmentId}/download", ct);
            var temp = Path.Combine(Path.GetTempPath(), $"sqd_{attachmentId:N}.jpg");
            await File.WriteAllBytesAsync(temp, bytes, ct);
            return temp;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string BuildSearchQuery(QuerySearchPayload p)
    {
        var parts = new List<string>
        {
            $"page={p.Page}",
            $"pageSize={p.PageSize}",
            $"sortBy={p.SortBy ?? "DelayDays"}",
            $"sortDirection={p.SortDirection ?? "desc"}"
        };
        if (!string.IsNullOrWhiteSpace(p.IPO)) parts.Add($"ipo={Uri.EscapeDataString(p.IPO)}");
        if (!string.IsNullOrWhiteSpace(p.Keyword)) parts.Add($"keyword={Uri.EscapeDataString(p.Keyword)}");
        if (p.ProjectId.HasValue) parts.Add($"projectId={p.ProjectId}");
        if (p.IssueTypeId.HasValue) parts.Add($"issueTypeId={p.IssueTypeId}");
        if (p.Status.HasValue) parts.Add($"status={p.Status}");
        if (p.DateFrom.HasValue) parts.Add($"dateFrom={p.DateFrom:O}");
        if (p.DateTo.HasValue) parts.Add($"dateTo={p.DateTo:O}");
        if (p.MineOnly.HasValue) parts.Add($"mineOnly={p.MineOnly.Value.ToString().ToLowerInvariant()}");
        return string.Join('&', parts);
    }
}

public sealed class DashboardService(ApiClient api)
{
    public Task<DashboardSnapshotDto?> GetSnapshotAsync(CancellationToken ct = default)
        => api.GetAsync<DashboardSnapshotDto>("api/dashboard/snapshot", ct);
}

public sealed class ProductService(ApiClient api)
{
    public Task<PagedResult<ProductSummaryDto>?> SearchAsync(string? query = null, string? category = null,
        Guid? projectId = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var parts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(category)) parts.Add($"category={Uri.EscapeDataString(category)}");
        if (projectId.HasValue) parts.Add($"projectId={projectId}");
        return api.GetAsync<PagedResult<ProductSummaryDto>>($"api/products?{string.Join('&', parts)}", ct);
    }

    public Task<ProductDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
        => api.GetAsync<ProductDetailDto>($"api/products/{id}", ct);
}

public sealed class EmailService(ApiClient api)
{
    public Task<List<EmailTemplateDto>?> GetTemplatesAsync(CancellationToken ct = default)
        => api.GetAsync<List<EmailTemplateDto>>("api/email/templates", ct);

    public Task<GeneratedEmailDto?> GenerateAsync(Guid queryId, Guid? templateId = null, string? recipient = null, CancellationToken ct = default)
        => api.PostAsync<GeneratedEmailDto>("api/email/generate", new { queryId, templateId, recipient }, ct);

    public async Task<Guid> SendAsync(Guid queryId, string recipient, string subject, string body, Guid? templateId = null, CancellationToken ct = default)
    {
        Guid? id = await api.PostAsync<Guid>("api/email/send", new { queryId, recipient, subject, body, templateId }, ct);
        return id ?? Guid.Empty;
    }
}

public sealed class NotificationService(ApiClient api)
{
    public Task<List<NotificationDto>?> GetMineAsync(CancellationToken ct = default)
        => api.GetAsync<List<NotificationDto>>("api/notifications", ct);

    public Task MarkReadAsync(List<Guid>? ids = null, CancellationToken ct = default)
        => api.PostVoidAsync("api/notifications/read", new { ids }, ct);
}

public sealed class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string Type { get; set; } = "Info";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? EntityId { get; set; }
}