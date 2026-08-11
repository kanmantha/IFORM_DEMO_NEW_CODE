namespace SiteQueryDefectTracking.Application.Interfaces;

using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Domain.Entities;

/// <summary>
/// Database access abstraction used by the application layer. The concrete
/// implementation (EF Core DbContext) lives in the Infrastructure layer.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<User> Users { get; }
    DbSet<IssueType> IssueTypes { get; }
    DbSet<ProductCode> ProductCodes { get; }
    DbSet<ProductSpecification> ProductSpecifications { get; }
    DbSet<ProductProjectMapping> ProductProjectMappings { get; }
    DbSet<Slab> Slabs { get; }
    DbSet<Query> Queries { get; }
    DbSet<QueryComment> QueryComments { get; }
    DbSet<QueryStatusHistory> QueryStatusHistories { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailLog> EmailLogs { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}