namespace SiteQueryDefectTracking.Domain.Entities;

/// <summary>
/// Optional slab-level tracking aligned with the legacy tracker slab columns.
/// </summary>
public class Slab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Target { get; set; }
    public string? Completed { get; set; }
    public int? DelayDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Query> Queries { get; set; } = new List<Query>();
}