using System.ComponentModel.DataAnnotations;
using IForm.Domain.Enums;

namespace IForm.Application.DTOs;

public record ProjectDto(Guid Id, string ProjectCode, string ProjectName, string? Client, string? Location, DateTime? StartDate, DateTime? PlannedCompletion, ProjectStatus Status, string? AssignedManagerName);

public record ProjectListItemDto(Guid Id, string ProjectCode, string ProjectName, string? Client, string? Location, ProjectStatus Status, int OpenQueries, int ResolvedQueries, int TotalQueries);

public record IpoListItemDto(Guid Id, string IpoNumber, Guid ProjectId, string ProjectName, decimal? Quantity, decimal? QuantitySqm, DispatchStatus DispatchStatus, DateTime? SlabTargetCastingDate, DateTime? SlabCompletedDate, int? SlabDelayDays, int OpenQueries);

public class CreateProjectRequest
{
    [Required] public string ProjectCode { get; set; } = string.Empty;
    [Required] public string ProjectName { get; set; } = string.Empty;
    public string? Client { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? PlannedCompletion { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public Guid? AssignedManagerId { get; set; }
}

public class UpdateProjectRequest : CreateProjectRequest { }

public class CreateIpoRequest
{
    [Required] public string IpoNumber { get; set; } = string.Empty;
    [Required] public Guid ProjectId { get; set; }
    public string? Client { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? QuantitySqm { get; set; }
    public DispatchStatus DispatchStatus { get; set; } = DispatchStatus.Pending;
    public DateTime? SlabTargetCastingDate { get; set; }
    public DateTime? SlabCompletedDate { get; set; }
}
