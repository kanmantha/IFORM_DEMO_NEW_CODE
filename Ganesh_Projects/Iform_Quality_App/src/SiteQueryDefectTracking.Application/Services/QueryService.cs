using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Exceptions;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Application.Validators;
using SiteQueryDefectTracking.Domain.Constants;
using SiteQueryDefectTracking.Domain.Contracts;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Application.Services;

public class QueryService(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeService clock,
    IDelayCalculator delayCalculator,
    IAuditLogService auditLog,
    IDomainEventPublisher events,
    IFileStorageService storage) : IQueryService
{
    public async Task<Guid> CreateAsync(CreateQueryRequest request, CancellationToken ct = default)
    {
        var validationResult = await new CreateQueryRequestValidator().ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(
                validationResult.Errors.Select(e => (e.PropertyName, e.ErrorMessage)));
        }

        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            throw new UnauthorizedException();

        var projectExists = await context.Projects.AnyAsync(p => p.Id == request.ProjectId && p.IsActive, ct);
        if (!projectExists) throw new NotFoundException("Project", request.ProjectId);

        var issueTypeExists = await context.IssueTypes.AnyAsync(i => i.Id == request.IssueTypeId && i.IsActive, ct);
        if (!issueTypeExists) throw new NotFoundException("Issue type", request.IssueTypeId);

        if (request.VerifiedProductCodeId.HasValue)
        {
            var productExists = await context.ProductCodes.AnyAsync(p => p.Id == request.VerifiedProductCodeId.Value, ct);
            if (!productExists) throw new BusinessException("The verified product code is not part of the catalogue.");
        }

        var raiseDate = request.RaiseDate ?? clock.AppNow;

        var entity = new Query
        {
            QueryNo = await GenerateQueryNoAsync(ct),
            IPO = request.IPO.Trim(),
            ProjectId = request.ProjectId,
            IssueTypeId = request.IssueTypeId,
            VerifiedProductCodeId = request.VerifiedProductCodeId,
            ProductCodeText = request.ProductCodeText?.Trim(),
            QuantityNos = request.QuantityNos,
            QuantitySqm = request.QuantitySqm,
            DispatchStatus = ParseDispatchStatus(request.DispatchStatus),
            Description = request.Description,
            SlabTarget = request.SlabTarget,
            SlabCompleted = request.SlabCompleted,
            SlabDelayDays = request.SlabDelayDays,
            RaiseDate = raiseDate,
            ResolvedDate = null,
            Status = QueryStatus.Pending,
            RaisedByUserId = currentUser.UserId!,
            DelayDays = delayCalculator.CalculateDelayDays(raiseDate, null)
        };

        context.Queries.Add(entity);
        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.QueryCreated, nameof(Query), entity.Id.ToString(),
            null, Serialize(request), currentUser.IpAddress, currentUser.DeviceInfo), ct);

        await events.PublishQueryCreatedAsync(entity.ToSummary(), ct);
        return entity.Id;
    }

    public async Task<QueryDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await context.Queries
            .AsNoTracking()
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.VerifiedProductCode)
            .Include(q => q.RaisedByUser)
            .Include(q => q.ResolvedByUser)
            .Include(q => q.Comments).ThenInclude(c => c.User)
            .Include(q => q.StatusHistory).ThenInclude(h => h.ChangedByUser)
            .Include(q => q.Attachments)
            .Include(q => q.Emails).ThenInclude(e => e.Template)
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        EnforceAccess(entity);
        return MapToDetail(entity);
    }

    public async Task<PagedResult<QuerySummaryDto>> SearchAsync(QuerySearchRequest request, CancellationToken ct = default)
    {
        var query = context.Queries.AsNoTracking();

        // Permission scoping: Site Engineers only ever see their own queries,
        // regardless of the MineOnly / RaisedByUserId filters (which are Manager-only).
        if (!currentUser.IsManager)
        {
            query = query.Where(q => q.RaisedByUserId == currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.IPO))
            query = query.Where(q => q.IPO.StartsWith(request.IPO.Trim()));

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(q =>
                q.IPO.Contains(keyword)
                || (q.Project != null && q.Project.Name.Contains(keyword))
                || (q.Description != null && q.Description.Contains(keyword))
                || (q.ProductCodeText != null && q.ProductCodeText.Contains(keyword))
                || (q.VerifiedProductCode != null && q.VerifiedProductCode.Code.Contains(keyword)));
        }

        if (request.ProjectId.HasValue) query = query.Where(q => q.ProjectId == request.ProjectId.Value);
        if (request.IssueTypeId.HasValue) query = query.Where(q => q.IssueTypeId == request.IssueTypeId.Value);
        if (request.Status.HasValue) query = query.Where(q => q.Status == request.Status.Value);
        if (request.DateFrom.HasValue) query = query.Where(q => q.RaiseDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue) query = query.Where(q => q.RaiseDate <= request.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(request.RaisedByUserId)) query = query.Where(q => q.RaisedByUserId == request.RaisedByUserId);

        var total = await query.CountAsync(ct);

        var sort = new SortDescriptor(request.SortBy, request.SortDirection);
        query = ApplySort(query, sort);

        var items = await query
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.VerifiedProductCode)
            .Include(q => q.RaisedByUser)
            .Include(q => q.ResolvedByUser)
            .Include(q => q.Attachments)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return PagedResult<QuerySummaryDto>.Create(
            items.Select(QueryMappers.ToSummary), total, request.Page, request.PageSize);
    }

    public async Task<QuerySummaryDto> UpdateAsync(Guid id, UpdateQueryRequest request, CancellationToken ct = default)
    {
        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        EnforceAccess(entity);
        if (entity.Status == QueryStatus.Resolved)
            throw new BusinessException("Resolved queries cannot be edited.");

        if (request.ProjectId.HasValue) entity.ProjectId = request.ProjectId.Value;
        if (request.IssueTypeId.HasValue) entity.IssueTypeId = request.IssueTypeId.Value;
        if (!string.IsNullOrWhiteSpace(request.IPO)) entity.IPO = request.IPO.Trim();
        if (request.QuantityNos.HasValue) entity.QuantityNos = request.QuantityNos;
        if (request.QuantitySqm.HasValue) entity.QuantitySqm = request.QuantitySqm;
        if (request.VerifiedProductCodeId.HasValue) entity.VerifiedProductCodeId = request.VerifiedProductCodeId;
        if (request.ProductCodeText is not null) entity.ProductCodeText = request.ProductCodeText.Trim();
        if (request.DispatchStatus is not null) entity.DispatchStatus = ParseDispatchStatus(request.DispatchStatus);
        if (request.Description is not null) entity.Description = request.Description;
        if (request.SlabTarget is not null) entity.SlabTarget = request.SlabTarget;
        if (request.SlabCompleted is not null) entity.SlabCompleted = request.SlabCompleted;
        if (request.SlabDelayDays.HasValue) entity.SlabDelayDays = request.SlabDelayDays;

        entity.UpdatedAt = clock.AppNow;
        entity.DelayDays = delayCalculator.CalculateDelayDays(entity.RaiseDate, entity.ResolvedDate);

        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.QueryUpdated, nameof(Query), id.ToString(),
            null, Serialize(request), currentUser.IpAddress, currentUser.DeviceInfo), ct);

        var summary = await BuildSummaryAsync(id, ct);
        await events.PublishQueryUpdatedAsync(summary, ct);
        return summary;
    }

    public async Task<QuerySummaryDto> ChangeStatusAsync(Guid id, ChangeQueryStatusRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsManager)
            throw new ForbiddenException("Only a Manager can change the query status.");

        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        if (entity.Status == request.Status)
            throw new BusinessException($"Query is already {request.Status}.");

        if (!IsValidTransition(entity.Status, request.Status))
            throw new BusinessException($"Invalid status transition from '{entity.Status}' to '{request.Status}'.");

        var from = entity.Status;
        entity.Status = request.Status;
        entity.UpdatedAt = clock.AppNow;

        context.QueryStatusHistories.Add(new QueryStatusHistory
        {
            QueryId = id,
            FromStatus = from,
            ToStatus = request.Status,
            ChangedByUserId = currentUser.UserId!,
            ChangedAt = clock.AppNow,
            Reason = request.Reason
        });

        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.QueryStatusChanged, nameof(Query), id.ToString(),
            from.ToString(), request.Status.ToString(), currentUser.IpAddress, currentUser.DeviceInfo), ct);

        var summary = await BuildSummaryAsync(id, ct);
        await events.PublishQueryStatusChangedAsync(summary, ct);
        return summary;
    }

    public async Task<Guid> ResolveAsync(Guid id, ResolveQueryRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsManager)
            throw new ForbiddenException("Only a Manager can resolve a query.");

        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        if (entity.Status == QueryStatus.Resolved)
            throw new BusinessException("Query is already resolved.");

        var managerId = currentUser.UserId!;
        var resolvedAt = clock.AppNow;
        var fromStatus = entity.Status;

        entity.Status = QueryStatus.Resolved;
        entity.ResolvedDate = resolvedAt;
        entity.ResolvedByUserId = managerId;
        entity.DelayDays = delayCalculator.CalculateDelayDays(entity.RaiseDate, resolvedAt);
        entity.UpdatedAt = resolvedAt;

        context.QueryStatusHistories.Add(new QueryStatusHistory
        {
            QueryId = id,
            FromStatus = fromStatus,
            ToStatus = QueryStatus.Resolved,
            ChangedByUserId = managerId,
            ChangedAt = resolvedAt,
            Reason = request.ResolutionNote
        });

        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            managerId, AuditActions.QueryResolved, nameof(Query), id.ToString(),
            "Open", "Resolved", currentUser.IpAddress, currentUser.DeviceInfo), ct);

        var summary = await BuildSummaryAsync(id, ct);
        await events.PublishQueryResolvedAsync(summary, ct);
        return id;
    }

    public async Task<CommentDto> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken ct = default)
    {
        var validationResult = await new CommentRequestValidator().ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(
                validationResult.Errors.Select(e => (e.PropertyName, e.ErrorMessage)));
        }

        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        EnforceAccess(entity);
        if (entity.Status == QueryStatus.Resolved)
            throw new BusinessException("Comments cannot be added to a resolved query.");

        var comment = new QueryComment
        {
            QueryId = id,
            UserId = currentUser.UserId!,
            CommentText = request.CommentText.Trim()
        };

        context.QueryComments.Add(comment);
        await context.SaveChangesAsync(ct);

        var dto = new CommentDto(comment.Id, comment.QueryId, comment.UserId,
            currentUser.UserName ?? string.Empty, comment.CommentText, comment.CreatedAt);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.CommentAdded, nameof(Query), id.ToString(),
            null, comment.CommentText, currentUser.IpAddress, currentUser.DeviceInfo), ct);

        await events.PublishCommentAddedAsync(id, dto, ct);
        return dto;
    }

    public async Task<IReadOnlyList<CommentDto>> GetCommentsAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        EnforceAccess(entity);

        return await context.QueryComments
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.QueryId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.QueryId, c.UserId,
                c.User != null ? c.User.FullName : string.Empty, c.CommentText, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AttachmentDto> AddAttachmentAsync(Guid id, Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        EnforceAccess(entity);

        var stored = await storage.SaveAsync(stream, "query-photos", fileName, contentType, ct);
        var attachment = new Attachment
        {
            QueryId = id,
            UploadedByUserId = currentUser.UserId!,
            Type = AttachmentType.Photo,
            OriginalFileName = fileName,
            StoredFileName = Path.GetFileName(stored.StorageKey),
            ContentType = stored.ContentType,
            Size = stored.Size,
            Width = stored.Width,
            Height = stored.Height,
            StoragePath = stored.StorageKey,
            UploadedAt = clock.AppNow,
            RetentionExpiry = clock.AppNow.AddDays(AppDefaults.PhotoRetentionDays)
        };

        context.Attachments.Add(attachment);
        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.AttachmentUploaded, nameof(Attachment), attachment.Id.ToString(),
            null, fileName, currentUser.IpAddress, currentUser.DeviceInfo), ct);

        return new AttachmentDto(
            attachment.Id, attachment.QueryId, attachment.OriginalFileName, attachment.ContentType,
            attachment.Size, attachment.Width, attachment.Height, attachment.Type.ToString(),
            attachment.UploadedAt, attachment.UploadedByUserId,
            $"/api/queries/{attachment.QueryId}/attachments/{attachment.Id}/download");
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var entity = await context.Queries.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new NotFoundException("Query", id);

        EnforceAccess(entity);

        var attachment = await context.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.QueryId == id, ct)
            ?? throw new NotFoundException("Attachment", attachmentId);

        var stream = await storage.OpenReadAsync(attachment.StoragePath, ct);
        if (stream is null) throw new NotFoundException("Attachment file", attachmentId);

        return (stream, attachment.ContentType, attachment.OriginalFileName);
    }

    private static IQueryable<Query> ApplySort(IQueryable<Query> query, SortDescriptor sort)
    {
        var by = sort.AppliedSortBy?.ToLowerInvariant();
        return by switch
        {
            "ipo" => sort.IsDescending ? query.OrderByDescending(q => q.IPO) : query.OrderBy(q => q.IPO),
            "status" => sort.IsDescending ? query.OrderByDescending(q => q.Status) : query.OrderBy(q => q.Status),
            "raisedate" or "raise" => sort.IsDescending ? query.OrderByDescending(q => q.RaiseDate) : query.OrderBy(q => q.RaiseDate),
            "project" => sort.IsDescending ? query.OrderByDescending(q => q.Project!.Name) : query.OrderBy(q => q.Project!.Name),
            "issuetype" => sort.IsDescending ? query.OrderByDescending(q => q.IssueType!.Name) : query.OrderBy(q => q.IssueType!.Name),
            "raisedby" => sort.IsDescending ? query.OrderByDescending(q => q.RaisedByUser!.FullName) : query.OrderBy(q => q.RaisedByUser!.FullName),
            _ => sort.IsDescending ? query.OrderByDescending(q => q.DelayDays) : query.OrderBy(q => q.DelayDays)
        };
    }

    private void EnforceAccess(Query entity)
    {
        if (!currentUser.IsManager && entity.RaisedByUserId != currentUser.UserId)
            throw new ForbiddenException("You do not have access to this query.");
    }

    private static bool IsValidTransition(QueryStatus from, QueryStatus to) => from switch
    {
        QueryStatus.Pending => to == QueryStatus.InProgress,
        QueryStatus.InProgress => to == QueryStatus.Resolved || to == QueryStatus.Pending,
        _ => false
    };

    private static DispatchStatus ParseDispatchStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DispatchStatus.NotDispatched;
        return Enum.TryParse<DispatchStatus>(value, true, out var parsed) ? parsed : DispatchStatus.NotDispatched;
    }

    private async Task<string> GenerateQueryNoAsync(CancellationToken ct)
    {
        var now = clock.AppNow;
        var prefix = $"SQ-{now.Year}{now.Month:00}";
        var values = await context.Queries
            .Where(q => q.QueryNo.StartsWith(prefix))
            .Select(q => q.QueryNo)
            .ToListAsync(ct);

        var max = values
            .Select(v => v.Length > prefix.Length + 1
                && int.TryParse(v.AsSpan(prefix.Length + 1), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{max + 1:D4}";
    }

    private async Task<QuerySummaryDto> BuildSummaryAsync(Guid id, CancellationToken ct)
    {
        var entity = await context.Queries
            .AsNoTracking()
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.RaisedByUser)
            .Include(q => q.ResolvedByUser)
            .Include(q => q.VerifiedProductCode)
            .Include(q => q.Attachments)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        return entity is null ? throw new NotFoundException("Query", id) : QueryMappers.ToSummary(entity);
    }

    private static QueryDetailDto MapToDetail(Query entity)
    {
        return new QueryDetailDto(
            entity.Id, entity.IPO, entity.ProjectId, entity.Project?.Name ?? string.Empty,
            entity.IssueTypeId, entity.IssueType?.Name ?? string.Empty, entity.IssueType?.Code,
            entity.Status, entity.QuantityNos ?? 0, entity.QuantitySqm,
            entity.VerifiedProductCodeId,
            entity.VerifiedProductCode?.Code ?? entity.ProductCodeText,
            entity.DispatchStatus.ToString(), entity.RaisedByUserId,
            entity.RaisedByUser?.FullName ?? string.Empty, entity.ResolvedByUserId,
            entity.ResolvedByUser?.FullName, entity.RaiseDate, entity.ResolvedDate,
            entity.DelayDays, entity.SlabTarget, entity.SlabCompleted, entity.SlabDelayDays,
            entity.Attachments.Count, entity.Description)
        {
            Description = entity.Description,
            Comments = entity.Comments.Select(QueryMappers.ToComment).ToList(),
            StatusHistory = entity.StatusHistory
                .OrderByDescending(h => h.ChangedAt)
                .Select(QueryMappers.ToStatusHistory)
                .ToList(),
            Attachments = entity.Attachments.Select(a => new AttachmentDto(
                a.Id, a.QueryId, a.OriginalFileName, a.ContentType, a.Size, a.Width, a.Height,
                a.Type.ToString(), a.UploadedAt, a.UploadedByUserId,
                $"/api/queries/{entity.Id}/attachments/{a.Id}/download")).ToList(),
            Emails = entity.Emails.Select(e => new EmailLogDto(
                e.Id, e.QueryId, e.TemplateId, e.Template?.Name, e.Recipient, e.Sender, e.Subject,
                (EmailLogStatus)(int)e.Status, e.SentAt, e.ErrorMessage)).ToList(),
            Timeline = QueryTimelineBuilder.Build(entity)
        };
    }

    private static string Serialize(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}

internal static class QueryExtensions
{
    public static QuerySummaryDto ToSummary(this Query query) => QueryMappers.ToSummary(query);

    public static string ToDisplay(this QueryStatus status) => status switch
    {
        QueryStatus.Pending => "Pending",
        QueryStatus.InProgress => "In Progress",
        QueryStatus.Resolved => "Resolved",
        _ => status.ToString()
    };
}