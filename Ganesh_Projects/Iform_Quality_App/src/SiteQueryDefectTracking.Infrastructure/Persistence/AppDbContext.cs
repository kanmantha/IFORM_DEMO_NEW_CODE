using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Common;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<User, Microsoft.AspNetCore.Identity.IdentityRole, string>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<IssueType> IssueTypes => Set<IssueType>();
    public DbSet<ProductCode> ProductCodes => Set<ProductCode>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<ProductProjectMapping> ProductProjectMappings => Set<ProductProjectMapping>();
    public DbSet<Query> Queries => Set<Query>();
    public DbSet<QueryComment> QueryComments => Set<QueryComment>();
    public DbSet<QueryStatusHistory> QueryStatusHistories => Set<QueryStatusHistory>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Slab> Slabs => Set<Slab>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureUsers(builder);
        ConfigureProjects(builder);
        ConfigureIssueTypes(builder);
        ConfigureProducts(builder);
        ConfigureQueries(builder);
        ConfigureEmails(builder);
        ConfigureAuditLogs(builder);
        ConfigureSecurity(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = ChangeTracker.Entries<AuditableEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            entry.Entity.UpdatedAt = now;
        }
    }

    private static void ConfigureUsers(ModelBuilder builder)
    {
        builder.Entity<User>()
            .Ignore(u => u.FullName)
            .Property(u => u.FirstName).HasMaxLength(80);
        builder.Entity<User>()
            .Property(u => u.LastName).HasMaxLength(80);

        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.UserId);
            e.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasIndex(n => n.UserId);
            e.HasIndex(n => n.IsRead);
            e.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureProjects(ModelBuilder builder)
    {
        builder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(50).IsRequired();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.ClientName).HasMaxLength(200);
            e.Property(p => p.Location).HasMaxLength(300);
        });
    }

    private static void ConfigureIssueTypes(ModelBuilder builder)
    {
        builder.Entity<IssueType>(e =>
        {
            e.HasIndex(i => i.Code).IsUnique();
            e.Property(i => i.Code).HasMaxLength(60).IsRequired();
            e.Property(i => i.Name).HasMaxLength(120).IsRequired();
        });
    }

    private static void ConfigureProducts(ModelBuilder builder)
    {
        builder.Entity<ProductCode>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => p.IsActive);
            e.Property(p => p.Code).HasMaxLength(80).IsRequired();
            e.Property(p => p.Name).HasMaxLength(300).IsRequired();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.Category).HasMaxLength(120);
            e.Property(p => p.Unit).HasMaxLength(60);
            e.Property(p => p.Barcode).HasMaxLength(120);

            e.HasMany(p => p.Specifications)
                .WithOne(s => s.ProductCode)
                .HasForeignKey(s => s.ProductCodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(p => p.ProjectMappings)
                .WithOne(m => m.ProductCode)
                .HasForeignKey(m => m.ProductCodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductSpecification>(e =>
        {
            e.HasIndex(s => s.ProductCodeId);
            e.Property(s => s.AttributeName).HasMaxLength(150).IsRequired();
            e.Property(s => s.AttributeValue).HasMaxLength(500);
        });

        builder.Entity<ProductProjectMapping>(e =>
        {
            e.HasIndex(m => new { m.ProductCodeId, m.ProjectId }).IsUnique();
            e.HasOne(m => m.Project)
                .WithMany(p => p.ProductMappings)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.ClientNoAction);
        });

        builder.Entity<Slab>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(120).IsRequired();
        });
    }

    private static void ConfigureQueries(ModelBuilder builder)
    {
        builder.Entity<Query>(e =>
        {
            e.Property(q => q.IPO).HasMaxLength(80).IsRequired();
            e.Property(q => q.QueryNo).HasMaxLength(40).IsRequired();
            e.HasIndex(q => q.QueryNo).IsUnique();
            e.HasIndex(q => q.IPO);
            e.HasIndex(q => q.ProjectId);
            e.HasIndex(q => q.IssueTypeId);
            e.HasIndex(q => q.Status);
            e.HasIndex(q => q.RaiseDate);
            e.HasIndex(q => q.RaisedByUserId);
            e.HasIndex(q => q.VerifiedProductCodeId);
            e.HasIndex(q => q.DelayDays);

            e.Property(q => q.Description).HasMaxLength(4000);

            e.HasOne(q => q.Project)
                .WithMany(p => p.Queries)
                .HasForeignKey(q => q.ProjectId);

            e.HasOne(q => q.IssueType)
                .WithMany(i => i.Queries)
                .HasForeignKey(q => q.IssueTypeId);

            e.HasOne(q => q.RaisedByUser)
                .WithMany()
                .HasForeignKey(q => q.RaisedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(q => q.ResolvedByUser)
                .WithMany()
                .HasForeignKey(q => q.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(q => q.VerifiedProductCode)
                .WithMany()
                .HasForeignKey(q => q.VerifiedProductCodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<QueryComment>(e =>
        {
            e.HasIndex(c => c.QueryId);
            e.Property(c => c.CommentText).HasMaxLength(2000).IsRequired();
            e.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QueryStatusHistory>(e =>
        {
            e.HasIndex(h => h.QueryId);
            e.HasIndex(h => h.ChangedAt);
            e.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(h => h.Reason).HasMaxLength(1000);
        });

        builder.Entity<Attachment>(e =>
        {
            e.HasIndex(a => a.QueryId);
            e.HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.StoragePath).HasMaxLength(500).IsRequired();
            e.Property(a => a.OriginalFileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        });
    }

    private static void ConfigureEmails(ModelBuilder builder)
    {
        builder.Entity<EmailTemplate>(e =>
        {
            e.HasIndex(t => t.Code).IsUnique();
            e.Property(t => t.Code).HasMaxLength(60).IsRequired();
            e.Property(t => t.Name).HasMaxLength(120).IsRequired();
            e.HasOne(t => t.IssueType)
                .WithMany()
                .HasForeignKey(t => t.IssueTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EmailLog>(e =>
        {
            e.HasIndex(l => l.QueryId);
            e.HasIndex(l => l.Status);
            e.HasIndex(l => l.SentAt);
            e.HasOne(l => l.Template)
                .WithMany()
                .HasForeignKey(l => l.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(l => new { l.EntityName, l.EntityId });
            e.HasIndex(l => l.Timestamp);
            e.HasIndex(l => l.Action);
            e.Property(l => l.Action).HasMaxLength(100).IsRequired();
            e.Property(l => l.EntityName).HasMaxLength(120).IsRequired();
            e.Property(l => l.Username).HasMaxLength(200);
        });
    }

    private static void ConfigureSecurity(ModelBuilder builder)
    {
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(80);
        });
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>(e =>
        {
            e.HasIndex(r => new { r.UserId, r.RoleId });
        });
    }
}