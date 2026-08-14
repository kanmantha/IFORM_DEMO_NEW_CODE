using System.Text.Json;
using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using IForm.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IEotService
{
    Task<Guid> CreateAsync(CreateEotRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateEotRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EotListItemDto>> GetAllAsync(Guid? projectId = null, EotStatus? status = null, CancellationToken ct = default);
    Task<EotDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> AddScopeVariationAsync(Guid eotId, AddScopeVariationRequest request, CancellationToken ct = default);
    Task DeleteScopeVariationAsync(Guid eotId, Guid scopeVariationId, CancellationToken ct = default);
    Task<Guid> SubmitAsync(Guid eotId, CancellationToken ct = default);
    Task TransitionAsync(Guid eotId, EotTransitionRequest request, CancellationToken ct = default);
    Task<bool> HasRequiredDocumentsAsync(Guid eotId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCategory>> GetMissingDocumentsAsync(Guid eotId, CancellationToken ct = default);
}

public class EotService : IEotService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notifications;
    private readonly ISubscriptionService _subscriptions;

    public EotService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IAuditLogger audit,
        INotificationService notifications,
        ISubscriptionService subscriptions)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notifications = notifications;
        _subscriptions = subscriptions;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");
    private Guid UserId => _currentUser.UserId ?? throw new AuthorizationException("Authentication required.");

    public async Task<Guid> CreateAsync(CreateEotRequest request, CancellationToken ct = default)
    {
        var currentPlanId = await GetCurrentPlanIdAsync(ct);
        var plan = (await _subscriptions.GetAvailablePlansAsync(ct)).FirstOrDefault(p => p.Id == currentPlanId);
        if (plan != null && !plan.AllowEot)
            throw new PlanLimitExceededException("Your subscription plan does not include EOT management.");

        var eot = new EotRecord
        {
            TenantId = Tenant,
            EotNumber = await GenerateEotNumberAsync(Tenant, ct),
            ProjectId = request.ProjectId,
            ClientEotNumber = request.ClientEotNumber,
            FinancialYear = request.FinancialYear,
            RevisionNumber = request.RevisionNumber,
            Scenario = request.Scenario,
            Category = request.Category,
            SpaDate = request.SpaDate,
            DesignRevisionDate = request.DesignRevisionDate,
            DelayDays = request.DelayDays,
            CostEscalation = request.CostEscalation,
            Reason = request.Reason,
            Reference = request.Reference,
            ChangeProposedBy = request.ChangeProposedBy,
            EstimatedTimeImpactDays = request.EstimatedTimeImpactDays,
            EstimatedCostImpact = request.EstimatedCostImpact,
            Remarks = request.Remarks,
            Status = EotStatus.Draft,
            SubmissionStatus = EotSubmissionStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = UserId
        };

        _db.EotRecords.Add(eot);
        await _db.SaveChangesAsync(ct);

        _db.EotStatusHistory.Add(new EotStatusHistory
        {
            TenantId = Tenant,
            EotId = eot.Id,
            OldStatus = EotStatus.Draft,
            NewStatus = EotStatus.Draft,
            ChangedByUserId = UserId,
            Remarks = "EOT created"
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("EOT Created", nameof(EotRecord), eot.Id.ToString(), null, eot.EotNumber, ct);
        return eot.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateEotRequest request, CancellationToken ct = default)
    {
        var eot = await _db.EotRecords.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("EOT not found.");
        if (eot.Status == EotStatus.Approved || eot.Status == EotStatus.Cancelled)
            throw new DomainException("Approved or cancelled EOT records cannot be edited.");

        eot.ClientEotNumber = request.ClientEotNumber;
        eot.FinancialYear = request.FinancialYear;
        eot.RevisionNumber = request.RevisionNumber;
        eot.Scenario = request.Scenario;
        eot.Category = request.Category;
        eot.SpaDate = request.SpaDate;
        eot.DesignRevisionDate = request.DesignRevisionDate;
        eot.DelayDays = request.DelayDays;
        eot.CostEscalation = request.CostEscalation;
        eot.Reason = request.Reason;
        eot.Reference = request.Reference;
        eot.ChangeProposedBy = request.ChangeProposedBy;
        eot.EstimatedTimeImpactDays = request.EstimatedTimeImpactDays;
        eot.EstimatedCostImpact = request.EstimatedCostImpact;
        eot.Remarks = request.Remarks;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("EOT Updated", nameof(EotRecord), id.ToString(), null, eot.EotNumber, ct);
    }

    public async Task<IReadOnlyList<EotListItemDto>> GetAllAsync(Guid? projectId = null, EotStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.EotRecords
            .Include(x => x.Project)
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .AsNoTracking();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

        return list.Select(x => new EotListItemDto(x.Id, x.EotNumber, x.Project?.DisplayName ?? string.Empty,
            x.ClientEotNumber, x.FinancialYear, x.RevisionNumber, x.Scenario, x.Category, x.DelayDays, x.CostEscalation,
            x.Status, x.SubmissionStatus, x.ClientApproval, x.CreatedAt)).ToList();
    }

    public async Task<EotDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var eot = await _db.EotRecords
            .Include(x => x.Project)
            .Include(x => x.ScopeVariations)
            .Include(x => x.EotDocuments)
            .Include(x => x.StatusHistory).ThenInclude(h => h.ChangedByUser)
            .Include(x => x.ClientApprovals)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct);

        if (eot == null) return null;

        var hasRequired = await HasRequiredDocumentsAsync(id, ct);
        var missing = await GetMissingDocumentsAsync(id, ct);

        return new EotDetailDto(eot.Id, eot.EotNumber, eot.ProjectId, eot.Project?.DisplayName ?? string.Empty,
            eot.ClientEotNumber, eot.FinancialYear, eot.RevisionNumber, eot.Scenario, eot.Category, eot.SpaDate,
            eot.DesignRevisionDate, eot.DelayDays, eot.CostEscalation, eot.Status, eot.SubmissionStatus,
            eot.ClientApproval, eot.Reason, eot.Reference, eot.ChangeProposedBy, eot.EstimatedTimeImpactDays,
            eot.EstimatedCostImpact, eot.Remarks,
            eot.ScopeVariations.Select(v => new ScopeVariationDto(v.Id, v.OriginalApprovedScope, v.RevisedScope,
                v.ScopeAddition, v.ScopeReduction, v.RevisionReference, v.Unit, v.NetScopeVariation)).ToList(),
            eot.EotDocuments.Select(d => new EotDocumentDto(d.Id, d.Category, d.FileName, d.FilePath, d.ContentType, d.SizeBytes, d.UploadedAt)).ToList(),
            eot.StatusHistory.OrderBy(h => h.ChangedDateTime).Select(h => new EotStatusHistoryDto(h.OldStatus, h.NewStatus,
                h.ChangedByUser?.FullName ?? "Unknown", h.ChangedDateTime, h.Remarks)).ToList(),
            hasRequired);
    }

    public async Task<Guid> AddScopeVariationAsync(Guid eotId, AddScopeVariationRequest request, CancellationToken ct = default)
    {
        var eot = await _db.EotRecords.FirstOrDefaultAsync(x => x.Id == eotId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("EOT not found.");

        var variation = new ScopeVariation
        {
            TenantId = Tenant,
            EotId = eotId,
            OriginalApprovedScope = request.OriginalApprovedScope,
            RevisedScope = request.RevisedScope,
            ScopeAddition = request.ScopeAddition,
            ScopeReduction = request.ScopeReduction,
            RevisionReference = request.RevisionReference,
            Unit = request.Unit
        };
        _db.ScopeVariations.Add(variation);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Scope Variation Added", nameof(ScopeVariation), variation.Id.ToString(), null,
            $"Net: {variation.NetScopeVariation}", ct);
        return variation.Id;
    }

    public async Task DeleteScopeVariationAsync(Guid eotId, Guid scopeVariationId, CancellationToken ct = default)
    {
        var variation = await _db.ScopeVariations
            .FirstOrDefaultAsync(x => x.Id == scopeVariationId && x.EotId == eotId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Scope variation not found.");
        _db.ScopeVariations.Remove(variation);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Scope Variation Deleted", nameof(ScopeVariation), scopeVariationId.ToString(), null, null, ct);
    }

    public async Task<Guid> SubmitAsync(Guid eotId, CancellationToken ct = default)
    {
        var eot = await _db.EotRecords.FirstOrDefaultAsync(x => x.Id == eotId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("EOT not found.");

        if (eot.Status != EotStatus.Draft && eot.Status != EotStatus.ReturnedForCorrection)
            throw new DomainException("Only a draft or returned EOT can be submitted.");

        var hasRequired = await HasRequiredDocumentsAsync(eotId, ct);
        if (!hasRequired)
        {
            var missing = await GetMissingDocumentsAsync(eotId, ct);
            throw new DomainException($"Incomplete submission. Missing documents: {string.Join(", ", missing)}.");
        }

        await TransitionAsync(eotId, new EotTransitionRequest(EotStatus.Submitted, "Submitted for review"), ct);
        return eotId;
    }

    public async Task TransitionAsync(Guid eotId, EotTransitionRequest request, CancellationToken ct = default)
    {
        var eot = await _db.EotRecords.FirstOrDefaultAsync(x => x.Id == eotId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("EOT not found.");

        if (!EotBusinessRules.CanTransition(eot.Status, request.NewStatus))
            throw new DomainException($"Cannot move from {eot.Status} to {request.NewStatus}.");

        var old = eot.Status;
        eot.Status = request.NewStatus;
        eot.SubmissionStatus = request.NewStatus switch
        {
            EotStatus.Submitted => EotSubmissionStatus.Submitted,
            EotStatus.UnderReview => EotSubmissionStatus.UnderReview,
            EotStatus.ClientSignoffPending => EotSubmissionStatus.ClientSignoffPending,
            EotStatus.ContractsReview => EotSubmissionStatus.ContractsReview,
            EotStatus.Approved => EotSubmissionStatus.Approved,
            EotStatus.Rejected => EotSubmissionStatus.Rejected,
            _ => eot.SubmissionStatus
        };

        if (request.NewStatus == EotStatus.Approved) eot.ClientApproval = ClientApprovalStatus.Approved;
        if (request.NewStatus == EotStatus.Rejected) eot.ClientApproval = ClientApprovalStatus.Rejected;

        eot.Remarks = request.Remarks ?? eot.Remarks;

        _db.EotStatusHistory.Add(new EotStatusHistory
        {
            TenantId = Tenant,
            EotId = eotId,
            OldStatus = old,
            NewStatus = request.NewStatus,
            ChangedByUserId = UserId,
            Remarks = request.Remarks
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("EOT Status Changed", nameof(EotRecord), eotId.ToString(), old.ToString(), request.NewStatus.ToString(), ct);

        var type = request.NewStatus == EotStatus.Approved ? NotificationType.EotApproved
            : request.NewStatus == EotStatus.Rejected ? NotificationType.EotRejected
            : NotificationType.EotSubmitted;

        await _notifications.NotifyAsync(type, "EOT update", $"EOT {eot.EotNumber} is now {request.NewStatus}.",
            link: $"/Eot/Details/{eotId}", ct: ct);
    }

    public async Task<bool> HasRequiredDocumentsAsync(Guid eotId, CancellationToken ct = default)
    {
        var categories = await _db.EotDocuments
            .Where(d => d.EotId == eotId && d.TenantId == Tenant)
            .Select(d => d.Category)
            .Distinct()
            .ToListAsync(ct);
        return EotBusinessRules.HasRequiredDocuments(categories);
    }

    public async Task<IReadOnlyList<DocumentCategory>> GetMissingDocumentsAsync(Guid eotId, CancellationToken ct = default)
    {
        var categories = (await _db.EotDocuments
            .Where(d => d.EotId == eotId && d.TenantId == Tenant)
            .Select(d => d.Category)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        return EotBusinessRules.RequiredDocuments.Where(d => !categories.Contains(d)).ToList();
    }

    private async Task<string> GenerateEotNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var lastNumber = await _db.EotRecords
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.EotNumber)
            .ToListAsync(ct);

        var maxSeq = 0;
        foreach (var number in lastNumber)
        {
            if (number.StartsWith("EOT-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(number[4..], out var seq))
                maxSeq = Math.Max(maxSeq, seq);
        }

        return $"EOT-{maxSeq + 1:D2}";
    }

    private async Task<Guid?> GetCurrentPlanIdAsync(CancellationToken ct)
    {
        var sub = await _db.Subscriptions
            .Where(s => s.TenantId == Tenant)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.PlanId)
            .FirstOrDefaultAsync(ct);
        return sub == Guid.Empty ? null : sub;
    }
}

public interface IDocumentService
{
    Task<Guid> UploadAsync(Guid tenantId, string title, DocumentCategory category, Guid? projectId, Guid? queryId, Guid? eotId,
        string fileName, string contentType, byte[] content, Guid userId, CancellationToken ct = default);
    Task<Guid> UploadEotDocumentAsync(Guid eotId, DocumentCategory category, string fileName, string contentType, byte[] content, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Document?> GetAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class DocumentService : IDocumentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IFileStorageService _storage;

    public DocumentService(IApplicationDbContext db, ICurrentUser currentUser, IAuditLogger audit, IFileStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _storage = storage;
    }

    public async Task<Guid> UploadAsync(Guid tenantId, string title, DocumentCategory category, Guid? projectId, Guid? queryId, Guid? eotId,
        string fileName, string contentType, byte[] content, Guid userId, CancellationToken ct = default)
    {
        var stored = await _storage.SaveBytesAsync(content, fileName, contentType, "documents", ct);
        var doc = new Document
        {
            TenantId = tenantId,
            Title = title,
            Category = category,
            ProjectId = projectId,
            QueryId = queryId,
            EotId = eotId,
            FilePath = stored.Path,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = userId
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Document Uploaded", nameof(Document), doc.Id.ToString(), null, title, ct);
        return doc.Id;
    }

    public async Task<Guid> UploadEotDocumentAsync(Guid eotId, DocumentCategory category, string fileName, string contentType, byte[] content, CancellationToken ct = default)
    {
        var eot = await _db.EotRecords.FirstOrDefaultAsync(x => x.Id == eotId && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("EOT not found.");

        var stored = await _storage.SaveBytesAsync(content, fileName, contentType, "eot", ct);
        _db.EotDocuments.Add(new EotDocument
        {
            TenantId = eot.TenantId,
            EotId = eotId,
            Category = category,
            FilePath = stored.Path,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = UserId
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("EOT Document Uploaded", nameof(EotDocument), Guid.NewGuid().ToString(), null, fileName, ct);
        return Guid.Empty;
    }

    public async Task<IReadOnlyList<Document>> GetForProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await _db.Documents
            .Where(d => d.TenantId == Tenant && d.ProjectId == projectId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);

    public async Task<Document?> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == Tenant, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Document not found.");
        doc.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Document Deleted", nameof(Document), id.ToString(), null, doc.FileName, ct);
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");
    private Guid UserId => _currentUser.UserId ?? throw new AuthorizationException("Authentication required.");
}
