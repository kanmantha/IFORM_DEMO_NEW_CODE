namespace SiteQueryDefectTracking.Application.DTOs.Shared;

public record LookupItemDto(Guid Id, string Name, string? Code = null, bool IsActive = true)
{
    public static LookupItemDto FromProject(Domain.Entities.Project p) => new(p.Id, p.Name, p.Code, p.IsActive);
    public static LookupItemDto FromIssueType(Domain.Entities.IssueType it) => new(it.Id, it.Name, it.Code, it.IsActive);
}