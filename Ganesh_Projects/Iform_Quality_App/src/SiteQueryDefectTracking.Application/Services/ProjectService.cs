using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Shared;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Application.Services;

public class ProjectService(IApplicationDbContext context) : IProjectService
{
    public async Task<IReadOnlyList<LookupItemDto>> GetActiveAsync(CancellationToken ct = default)
    {
        return await context.Projects
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new LookupItemDto(p.Id, p.Name, p.Code, p.IsActive))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<LookupItemDto>> SearchAsync(string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Projects.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(p => p.Name.Contains(k) || (p.Code != null && p.Code.Contains(k)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new LookupItemDto(p.Id, p.Name, p.Code, p.IsActive))
            .ToListAsync(ct);

        return PagedResult<LookupItemDto>.Create(items, total, page, pageSize);
    }
}