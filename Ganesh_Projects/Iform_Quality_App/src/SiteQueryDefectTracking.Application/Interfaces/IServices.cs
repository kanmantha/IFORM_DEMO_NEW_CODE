using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Dashboard;
using SiteQueryDefectTracking.Application.DTOs.Email;
using SiteQueryDefectTracking.Application.DTOs.Notifications;
using SiteQueryDefectTracking.Application.DTOs.Products;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.DTOs.Reports;
using SiteQueryDefectTracking.Application.DTOs.Shared;

namespace SiteQueryDefectTracking.Application.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<LookupItemDto>> GetActiveAsync(CancellationToken ct = default);

    Task<PagedResult<LookupItemDto>> SearchAsync(string? keyword, int page, int pageSize, CancellationToken ct = default);
}

public interface IReferenceService
{
    Task<IReadOnlyList<LookupItemDto>> GetIssueTypesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<EnumOptionDto>> GetDispatchStatusesAsync(CancellationToken ct = default);
}

public interface IQueryService
{
    Task<Guid> CreateAsync(CreateQueryRequest request, CancellationToken ct = default);

    Task<QueryDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<QuerySummaryDto>> SearchAsync(QuerySearchRequest request, CancellationToken ct = default);

    Task<QuerySummaryDto> UpdateAsync(Guid id, UpdateQueryRequest request, CancellationToken ct = default);

    Task<QuerySummaryDto> ChangeStatusAsync(Guid id, ChangeQueryStatusRequest request, CancellationToken ct = default);

    Task<Guid> ResolveAsync(Guid id, ResolveQueryRequest request, CancellationToken ct = default);

    Task<CommentDto> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(Guid id, CancellationToken ct = default);

    Task<AttachmentDto> AddAttachmentAsync(Guid id, Stream stream, string fileName, string contentType, CancellationToken ct = default);

    Task<(Stream Stream, string ContentType, string FileName)> GetAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardSnapshotDto> GetSnapshotAsync(CancellationToken ct = default);

    Task<IReadOnlyList<QuerySummaryDto>> GetOpenQueriesAsync(CancellationToken ct = default);
}

public interface IProductService
{
    Task<PagedResult<ProductSummaryDto>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default);

    Task<ProductDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<ProductDetailDto> CreateAsync(CreateProductRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);

    Task<ProductDetailDto> UpdateAsync(Guid id, UpdateProductRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);

    Task<ProductImportSummary> ImportPreviewAsync(IReadOnlyList<ProductImportRow> rows, CancellationToken ct = default);

    Task<ProductImportSummary> CommitImportAsync(string jobId, CancellationToken ct = default);

    Task<ImportStatusDto?> GetImportStatusAsync(string jobId, CancellationToken ct = default);
}

public interface IEmailTemplateService
{
    Task<IReadOnlyList<EmailTemplateDto>> GetAllAsync(CancellationToken ct = default);

    Task<EmailTemplateDto> UpsertAsync(UpsertEmailTemplateRequest request, CancellationToken ct = default);
}

public interface IEmailService
{
    Task<GeneratedEmailDto> GenerateAsync(GenerateEmailRequest request, CancellationToken ct = default);

    Task<Guid> SendAsync(SendEmailRequest request, CancellationToken ct = default);

    Task<Guid> SendPreviewAsync(SendPreviewRequest request, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetMineAsync(CancellationToken ct = default);

    Task MarkReadAsync(MarkNotificationsReadRequest request, CancellationToken ct = default);
}

public interface IReportService
{
    Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default);
}