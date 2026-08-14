using IForm.Application.Common;
using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IProjectService
{
    Task<Guid> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<ProjectListItemDto>> SearchAsync(string? term, ProjectStatus? status, int page, int pageSize, CancellationToken ct = default);
}

public class ProjectService : IProjectService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public ProjectService(IApplicationDbContext db, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    public async Task<Guid> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var project = new Project
        {
            TenantId = Tenant,
            ProjectCode = request.ProjectCode.Trim(),
            ProjectName = request.ProjectName.Trim(),
            Client = request.Client,
            Location = request.Location,
            StartDate = request.StartDate,
            PlannedCompletion = request.PlannedCompletion,
            Status = request.Status,
            AssignedManagerId = request.AssignedManagerId,
            CreatedBy = _currentUser.UserName
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Project Created", nameof(Project), project.Id.ToString(), null, project.ProjectName, ct);
        return project.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Project not found.");

        project.ProjectCode = request.ProjectCode.Trim();
        project.ProjectName = request.ProjectName.Trim();
        project.Client = request.Client;
        project.Location = request.Location;
        project.StartDate = request.StartDate;
        project.PlannedCompletion = request.PlannedCompletion;
        project.Status = request.Status;
        project.AssignedManagerId = request.AssignedManagerId;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserName;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Project Updated", nameof(Project), id.ToString(), null, project.ProjectName, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("Project not found.");

        project.IsDeleted = true;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Project Deleted", nameof(Project), id.ToString(), null, project.ProjectName, ct);
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(x => x.AssignedManager)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct);
        return project is null
            ? null
            : new ProjectDto(project.Id, project.ProjectCode, project.ProjectName, project.Client, project.Location,
                project.StartDate, project.PlannedCompletion, project.Status, project.AssignedManager?.FullName);
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var projects = await _db.Projects
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Queries.Where(q => !q.IsDeleted))
            .AsNoTracking()
            .ToListAsync(ct);

        return projects
            .Select(p => new ProjectListItemDto(p.Id, p.ProjectCode, p.ProjectName, p.Client, p.Location, p.Status,
                p.Queries.Count(q => q.Status != QueryStatus.Resolved),
                p.Queries.Count(q => q.Status == QueryStatus.Resolved),
                p.Queries.Count))
            .ToList();
    }

    public async Task<PagedResult<ProjectListItemDto>> SearchAsync(string? term, ProjectStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Project> query = _db.Projects
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Queries.Where(q => !q.IsDeleted));

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(x => x.ProjectName.Contains(t) || x.ProjectCode.Contains(t) || (x.Client != null && x.Client.Contains(t)));
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var list = await query.AsNoTracking().ToListAsync(ct);
        var mapped = list.Select(p => new ProjectListItemDto(p.Id, p.ProjectCode, p.ProjectName, p.Client, p.Location, p.Status,
                p.Queries.Count(q => q.Status != QueryStatus.Resolved),
                p.Queries.Count(q => q.Status == QueryStatus.Resolved),
                p.Queries.Count)).ToList();

        return mapped.ToPaged(page, pageSize);
    }
}

public interface IIpoService
{
    Task<Guid> CreateAsync(CreateIpoRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<IpoListItemDto>> GetAllAsync(Guid? projectId = null, CancellationToken ct = default);
    Task<IReadOnlyList<IpoListItemDto>> SearchAsync(string? term, CancellationToken ct = default);
}

public class IpoService : IIpoService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public IpoService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    public async Task<Guid> CreateAsync(CreateIpoRequest request, CancellationToken ct = default)
    {
        var ipo = new Ipo
        {
            TenantId = Tenant,
            IpoNumber = request.IpoNumber.Trim(),
            ProjectId = request.ProjectId,
            Client = request.Client,
            Quantity = request.Quantity,
            QuantitySqm = request.QuantitySqm,
            DispatchStatus = request.DispatchStatus,
            SlabTargetCastingDate = request.SlabTargetCastingDate,
            SlabCompletedDate = request.SlabCompletedDate
        };
        _db.Ipos.Add(ipo);
        await _db.SaveChangesAsync(ct);
        return ipo.Id;
    }

    public async Task<IReadOnlyList<IpoListItemDto>> GetAllAsync(Guid? projectId = null, CancellationToken ct = default)
    {
        var query = _db.Ipos.Where(x => x.TenantId == Tenant)
            .Include(x => x.Project)
            .Include(x => x.Queries.Where(q => !q.IsDeleted))
            .AsNoTracking();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);

        var list = await query.ToListAsync(ct);
        return list.Select(x => new IpoListItemDto(x.Id, x.IpoNumber, x.ProjectId, x.Project?.DisplayName ?? string.Empty,
                x.Quantity, x.QuantitySqm, x.DispatchStatus, x.SlabTargetCastingDate, x.SlabCompletedDate, x.SlabDelayDays,
                x.Queries.Count(q => q.Status != QueryStatus.Resolved)))
            .ToList();
    }

    public async Task<IReadOnlyList<IpoListItemDto>> SearchAsync(string? term, CancellationToken ct = default)
    {
        var query = _db.Ipos.Where(x => x.TenantId == Tenant)
            .Include(x => x.Project)
            .Include(x => x.Queries.Where(q => !q.IsDeleted))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(x => x.IpoNumber.Contains(t) || (x.Project != null && x.Project.ProjectName.Contains(t)));
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return list.Select(x => new IpoListItemDto(x.Id, x.IpoNumber, x.ProjectId, x.Project?.DisplayName ?? string.Empty,
                x.Quantity, x.QuantitySqm, x.DispatchStatus, x.SlabTargetCastingDate, x.SlabCompletedDate, x.SlabDelayDays,
                x.Queries.Count(q => q.Status != QueryStatus.Resolved)))
            .ToList();
    }
}
