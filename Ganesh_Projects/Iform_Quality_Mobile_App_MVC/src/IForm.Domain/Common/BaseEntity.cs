using System.ComponentModel.DataAnnotations;

namespace IForm.Domain.Common;

/// <summary>
/// Base entity providing audit timestamps, soft delete and optimistic concurrency.
/// </summary>
public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Marker interface for entities that belong to a tenant.
/// Every business record must implement this so strict tenant isolation
/// can be enforced with global query filters.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
