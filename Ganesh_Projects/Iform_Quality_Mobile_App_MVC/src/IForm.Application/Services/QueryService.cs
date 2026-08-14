using System.Security.Claims;
using IForm.Application.Common;
using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using IForm.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IQueryService
{
    Task<Guid> CreateAsync(CreateQueryRequest request, IEnumerable<(byte[] Content, string FileName, string ContentType)>? photos, CancellationToken ct = default);
    Task<PagedResult<QueryListItemDto>> SearchAsync(QuerySearchRequest request, CancellationToken ct = default);
    Task<QueryDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddCommentAsync(Guid queryId, string body, CancellationToken ct = default);
    Task ChangeStatusAsync(Guid queryId, QueryStatus newStatus, string? comment, CancellationToken ct = default);
    Task<IReadOnlyList<QueryListItemDto>> GetRecentOpenAsync(int take = 10, CancellationToken ct = default);
    Task<QueryListItemDto> MapListItemAsync(SiteQuery q, int delayDays, CancellationToken ct = default);
}

public class QueryService : IQueryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notifications;
    private readonly ITenantSettingsProvider _settings;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _storage;

    public QueryService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IAuditLogger audit,
        INotificationService notifications,
        ITenantSettingsProvider settings,
        UserManager<ApplicationUser> userManager,
        IFileStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notifications = notifications;
        _settings = settings;
        _userManager = userManager;
        _storage = storage;
    }

    public async Task<Guid> CreateAsync(
        CreateQueryRequest request,
        IEnumerable<(byte[] Content, string FileName, string ContentType)>? photos,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var userId = RequireUser();

        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == request.ProjectId && x.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Project not found.");

        if (!string.IsNullOrWhiteSpace(request.IpoNumber))
        {
            var existingIpo = await _db.Ipos
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IpoNumber == request.IpoNumber, ct);
            if (existingIpo == null)
            {
                _db.Ipos.Add(new Ipo
                {
                    TenantId = tenantId,
                    IpoNumber = request.IpoNumber,
                    ProjectId = project.Id,
                    Client = project.Client,
                    DispatchStatus = request.DispatchStatus,
                    SlabTargetCastingDate = request.SlabTargetCastingDate,
                    SlabCompletedDate = request.SlabCompletedDate
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        var productId = request.ProductId;
        if (productId == null && !string.IsNullOrWhiteSpace(request.ProductCode))
        {
            productId = await _db.Products
                .Where(x => x.TenantId == tenantId && x.ProductCode == request.ProductCode.Trim() && x.IsActive)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(ct);
        }

        var query = new SiteQuery
        {
            TenantId = tenantId,
            QueryNumber = await GenerateQueryNumberAsync(tenantId, ct),
            IpoNumber = request.IpoNumber,
            IpoId = request.IpoId,
            ProjectId = project.Id,
            ProductId = productId,
            ProductCode = request.ProductCode,
            ProductName = request.ProductName,
            IssueType = request.IssueType,
            QuantityNos = request.QuantityNos,
            QuantitySqm = request.QuantitySqm,
            DispatchStatus = request.DispatchStatus,
            SlabTargetCastingDate = request.SlabTargetCastingDate,
            SlabCompletedDate = request.SlabCompletedDate,
            Status = QueryStatus.Pending,
            Comments = request.Comments,
            RaisedFrom = request.RaisedFrom,
            RaisedDate = DateTime.UtcNow,
            RaisedByUserId = userId,
            CreatedBy = _currentUser.UserName
        };

        _db.Queries.Add(query);
        await _db.SaveChangesAsync(ct);

        if (photos != null)
        {
            foreach (var photo in photos)
            {
                var stored = await _storage.SaveBytesAsync(photo.Content, photo.FileName, photo.ContentType, "queries", ct);
                _db.QueryPhotos.Add(new QueryPhoto
                {
                    TenantId = tenantId,
                    QueryId = query.Id,
                    FilePath = stored.Path,
                    FileName = stored.FileName,
                    ContentType = stored.ContentType,
                    SizeBytes = stored.SizeBytes,
                    UploadedByUserId = userId
                });
            }
        }

        _db.QueryStatusHistory.Add(new QueryStatusHistory
        {
            TenantId = tenantId,
            QueryId = query.Id,
            OldStatus = QueryStatus.Pending,
            NewStatus = QueryStatus.Pending,
            ChangedBy = userId,
            ChangedDateTime = DateTime.UtcNow,
            Comments = "Query raised"
        });

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Query Created", nameof(SiteQuery), query.Id.ToString(),
            null, $"{request.IssueType}|{request.IpoNumber}|{project.ProjectName}", ct);

        await _notifications.NotifyAsync(NotificationType.QueryCreated,
            "New query raised",
            $"Query {query.QueryNumber} ({request.IssueType} - {request.IpoNumber}) raised on {project.ProjectName}.",
            link: $"/Queries/Details/{query.Id}", ct: ct);

        return query.Id;
    }

    public async Task<PagedResult<QueryListItemDto>> SearchAsync(QuerySearchRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var today = DateTime.UtcNow;
        var thresholds = _settings.GetSeverityThresholds(tenantId);

        var q = _db.Queries
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.Project)
            .Include(x => x.RaisedByUser)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            q = q.Where(x =>
                x.IpoNumber.Contains(term) ||
                x.Project!.ProjectName.Contains(term) ||
                x.Project.ProjectCode.Contains(term) ||
                x.QueryNumber.Contains(term) ||
                (x.ProductCode != null && x.ProductCode.Contains(term)) ||
                (x.ProductName != null && x.ProductName.Contains(term)) ||
                (x.Comments != null && x.Comments.Contains(term)) ||
                (x.RaisedByUser != null && x.RaisedByUser.FullName.Contains(term)));
        }

        if (request.ProjectId.HasValue) q = q.Where(x => x.ProjectId == request.ProjectId.Value);
        if (request.IpoId.HasValue) q = q.Where(x => x.IpoId == request.IpoId.Value);
        if (request.ProductId.HasValue) q = q.Where(x => x.ProductId == request.ProductId.Value);
        if (request.IssueType.HasValue) q = q.Where(x => x.IssueType == request.IssueType.Value);
        if (request.Status.HasValue) q = q.Where(x => x.Status == request.Status.Value);
        if (request.RaisedById.HasValue) q = q.Where(x => x.RaisedByUserId == request.RaisedById.Value);
        if (request.DateFrom.HasValue) q = q.Where(x => x.RaisedDate.Date >= request.DateFrom.Value.Date);
        if (request.DateTo.HasValue) q = q.Where(x => x.RaisedDate.Date <= request.DateTo.Value.Date);
        if (request.MyQueries) q = q.Where(x => x.RaisedByUserId == _currentUser.UserId);

        var delayThresholds = new DelayThresholds(thresholds.Watch, thresholds.Delayed, thresholds.Critical, thresholds.Severe);
        var list = await q.ToListAsync(ct);

        var mapped = list.Select(x =>
        {
            var delay = QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today);
            return new QueryListItemDto(
                x.Id, x.QueryNumber, x.IpoNumber, x.Project?.DisplayName ?? string.Empty,
                x.ProductCode, x.ProductName, x.IssueType, x.QuantityNos, x.QuantitySqm,
                x.DispatchStatus, x.Status, delay, QueryBusinessRules.ClassifySeverity(delay, delayThresholds),
                x.RaisedByUser?.FullName ?? "Unknown", x.RaisedDate, x.ResolvedDate);
        }).AsQueryable();

        if (request.MinDelayDays.HasValue) mapped = mapped.Where(x => x.DelayDays >= request.MinDelayDays.Value);
        if (request.MaxDelayDays.HasValue) mapped = mapped.Where(x => x.DelayDays <= request.MaxDelayDays.Value);

        mapped = request.SortBy switch
        {
            "ipo" => request.SortDescending ? mapped.OrderByDescending(x => x.IpoNumber) : mapped.OrderBy(x => x.IpoNumber),
            "project" => request.SortDescending ? mapped.OrderByDescending(x => x.ProjectName) : mapped.OrderBy(x => x.ProjectName),
            "raised" => request.SortDescending ? mapped.OrderByDescending(x => x.RaisedDate) : mapped.OrderBy(x => x.RaisedDate),
            "status" => request.SortDescending ? mapped.OrderByDescending(x => x.Status) : mapped.OrderBy(x => x.Status),
            "severity" => request.SortDescending ? mapped.OrderByDescending(x => x.Severity) : mapped.OrderBy(x => x.Severity),
            _ => request.SortDescending ? mapped.OrderByDescending(x => x.DelayDays) : mapped.OrderBy(x => x.DelayDays)
        };

        return mapped.ToPaged(request.Page, request.PageSize);
    }

    public async Task<QueryDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var today = DateTime.UtcNow;
        var thresholds = _settings.GetSeverityThresholds(tenantId);

        var query = await _db.Queries
            .Include(x => x.Project)
            .Include(x => x.Ipo)
            .Include(x => x.Product)
            .Include(x => x.RaisedByUser)
            .Include(x => x.Photos)
            .Include(x => x.QueryComments).ThenInclude(c => c.Author)
            .Include(x => x.StatusHistory).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Query not found.");

        var audits = await _db.AuditLogs
            .Include(a => a.User)
            .Where(a => a.TenantId == tenantId && a.EntityType == nameof(SiteQuery) && a.EntityId == id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .ToListAsync(ct);

        var delay = QueryBusinessRules.CalculateDelayDays(query.RaisedDate, query.ResolvedDate, today);
        var severity = QueryBusinessRules.ClassifySeverity(delay, new DelayThresholds(thresholds.Watch, thresholds.Delayed, thresholds.Critical, thresholds.Severe));

        return new QueryDetailDto(
            query.Id, query.QueryNumber, query.IpoNumber, query.IpoId, query.ProjectId, query.Project?.DisplayName ?? string.Empty,
            query.ProductId, query.ProductCode, query.ProductName, query.Product?.Description, query.Product?.Specification,
            query.IssueType, query.QuantityNos, query.QuantitySqm, query.DispatchStatus,
            query.SlabTargetCastingDate, query.SlabCompletedDate, query.Ipo?.SlabDelayDays,
            query.Status, query.StatusComment, delay, severity, query.Comments,
            query.RaisedByUserId, query.RaisedByUser?.FullName ?? "Unknown", query.RaisedDate, query.ResolvedDate,
            query.Photos.Select(p => new PhotoDto(p.Id, p.FilePath, p.FileName, p.ContentType, p.SizeBytes, p.UploadedAt, p.FilePath)).ToList(),
            query.QueryComments.OrderByDescending(c => c.CreatedAt).Select(c => new CommentDto(c.Id, c.Body, c.Author?.FullName ?? "Unknown", c.CreatedAt)).ToList(),
            query.StatusHistory.OrderBy(h => h.ChangedDateTime).Select(h => new StatusHistoryDto(h.OldStatus, h.NewStatus, h.ChangedByUser?.FullName ?? "Unknown", h.ChangedDateTime, h.Comments)).ToList(),
            audits.Select(a => new AuditDto(a.Id, a.Action, a.EntityType, a.EntityId, a.OldValue, a.NewValue, a.User?.UserName, a.Timestamp)).ToList());
    }

    public async Task AddCommentAsync(Guid queryId, string body, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var userId = RequireUser();

        var query = await _db.Queries.FirstOrDefaultAsync(x => x.Id == queryId && x.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Query not found.");

        _db.QueryComments.Add(new QueryComment
        {
            TenantId = tenantId,
            QueryId = queryId,
            Body = body,
            AuthorId = userId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Comment Added", nameof(SiteQuery), queryId.ToString(), null, body, ct);
        await _notifications.NotifyAsync(NotificationType.CommentAdded, "Comment added",
            $"{_currentUser.FullName} commented on {query.QueryNumber}.", link: $"/Queries/Details/{queryId}", ct: ct);
    }

    public async Task ChangeStatusAsync(Guid queryId, QueryStatus newStatus, string? comment, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var userId = RequireUser();

        var query = await _db.Queries.FirstOrDefaultAsync(x => x.Id == queryId && x.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Query not found.");

        if (query.Status == newStatus) return;

        if (newStatus == QueryStatus.Resolved && !_currentUser.IsInRole(AppRoles.Manager) && !_currentUser.IsInRole(AppRoles.TenantAdmin))
            throw new AuthorizationException("Only a Manager can resolve a query.");

        if (query.Status == QueryStatus.Resolved)
            throw new DomainException("A resolved query cannot be reopened.");

        var old = query.Status;
        query.Status = newStatus;
        query.StatusComment = comment ?? query.StatusComment;
        if (newStatus == QueryStatus.Resolved)
        {
            query.ResolvedDate = DateTime.UtcNow;
            query.AssignedToManagerId = userId;
        }
        query.UpdatedAt = DateTime.UtcNow;
        query.UpdatedBy = _currentUser.UserName;

        _db.QueryStatusHistory.Add(new QueryStatusHistory
        {
            TenantId = tenantId,
            QueryId = queryId,
            OldStatus = old,
            NewStatus = newStatus,
            ChangedBy = userId,
            ChangedDateTime = DateTime.UtcNow,
            Comments = comment
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(newStatus == QueryStatus.Resolved ? "Query Resolved" : "Status Changed",
            nameof(SiteQuery), queryId.ToString(), old.ToString(), newStatus.ToString(), ct);

        await _notifications.NotifyAsync(
            newStatus == QueryStatus.Resolved ? NotificationType.QueryResolved : NotificationType.StatusChanged,
            newStatus == QueryStatus.Resolved ? "Query resolved" : "Status changed",
            $"Query {query.QueryNumber} is now {newStatus}.", link: $"/Queries/Details/{queryId}", ct: ct);
    }

    public async Task<IReadOnlyList<QueryListItemDto>> GetRecentOpenAsync(int take = 10, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var today = DateTime.UtcNow;
        var thresholds = _settings.GetSeverityThresholds(tenantId);
        var delayThresholds = new DelayThresholds(thresholds.Watch, thresholds.Delayed, thresholds.Critical, thresholds.Severe);

        var list = await _db.Queries
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Status != QueryStatus.Resolved)
            .Include(x => x.Project)
            .Include(x => x.RaisedByUser)
            .AsNoTracking()
            .ToListAsync(ct);

        return list
            .Select(x =>
            {
                var delay = QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today);
                return new QueryListItemDto(x.Id, x.QueryNumber, x.IpoNumber, x.Project?.DisplayName ?? string.Empty,
                    x.ProductCode, x.ProductName, x.IssueType, x.QuantityNos, x.QuantitySqm, x.DispatchStatus, x.Status,
                    delay, QueryBusinessRules.ClassifySeverity(delay, delayThresholds),
                    x.RaisedByUser?.FullName ?? "Unknown", x.RaisedDate, x.ResolvedDate);
            })
            .OrderByDescending(x => x.DelayDays)
            .Take(take)
            .ToList();
    }

    public async Task<QueryListItemDto> MapListItemAsync(SiteQuery q, int delayDays, CancellationToken ct = default)
    {
        var thresholds = _settings.GetSeverityThresholds(q.TenantId);
        return new QueryListItemDto(q.Id, q.QueryNumber, q.IpoNumber, q.Project?.DisplayName ?? string.Empty,
            q.ProductCode, q.ProductName, q.IssueType, q.QuantityNos, q.QuantitySqm, q.DispatchStatus, q.Status,
            delayDays, QueryBusinessRules.ClassifySeverity(delayDays, new DelayThresholds(thresholds.Watch, thresholds.Delayed, thresholds.Critical, thresholds.Severe)),
            q.RaisedByUser?.FullName ?? "Unknown", q.RaisedDate, q.ResolvedDate);
    }

    private async Task<string> GenerateQueryNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var prefix = "SQ";
        var last = await _db.Queries
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.QueryNumber)
            .FirstOrDefaultAsync(ct);

        var seq = 1;
        if (!string.IsNullOrWhiteSpace(last) && int.TryParse(last.Replace(prefix + "-", string.Empty), out var lastNum))
            seq = lastNum + 1;
        else
            seq = (await _db.Queries.CountAsync(x => x.TenantId == tenantId, ct)) + 1;

        return $"{prefix}-{seq:D4}";
    }

    private Guid RequireTenant() =>
        _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    private Guid RequireUser() =>
        _currentUser.UserId ?? throw new AuthorizationException("Authentication required.");
}
