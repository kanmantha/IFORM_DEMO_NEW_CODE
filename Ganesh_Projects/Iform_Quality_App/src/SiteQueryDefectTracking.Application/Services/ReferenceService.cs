using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.DTOs.Shared;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Application.Services;

public class ReferenceService(IApplicationDbContext context) : IReferenceService
{
    public async Task<IReadOnlyList<LookupItemDto>> GetIssueTypesAsync(CancellationToken ct = default)
    {
        return await context.IssueTypes
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new LookupItemDto(i.Id, i.Name, i.Code, i.IsActive))
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<EnumOptionDto>> GetDispatchStatusesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<EnumOptionDto> result = Enum.GetValues<DispatchStatus>()
            .Select(s => new EnumOptionDto((int)s, s.ToString()))
            .ToList();
        return Task.FromResult(result);
    }
}