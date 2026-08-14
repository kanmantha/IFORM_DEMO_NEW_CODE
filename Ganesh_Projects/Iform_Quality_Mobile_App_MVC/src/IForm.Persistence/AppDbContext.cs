using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using IForm.Domain.Common;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IForm.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    private readonly ICurrentUser? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionTransaction> SubscriptionTransactions => Set<SubscriptionTransaction>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Ipo> Ipos => Set<Ipo>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductProjectMapping> ProductProjectMappings => Set<ProductProjectMapping>();
    public DbSet<SiteQuery> Queries => Set<SiteQuery>();
    public DbSet<QueryPhoto> QueryPhotos => Set<QueryPhoto>();
    public DbSet<QueryComment> QueryComments => Set<QueryComment>();
    public DbSet<QueryStatusHistory> QueryStatusHistory => Set<QueryStatusHistory>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailRecord> EmailRecords => Set<EmailRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<EotRecord> EotRecords => Set<EotRecord>();
    public DbSet<EotDocument> EotDocuments => Set<EotDocument>();
    public DbSet<EotStatusHistory> EotStatusHistory => Set<EotStatusHistory>();
    public DbSet<ScopeVariation> ScopeVariations => Set<ScopeVariation>();
    public DbSet<ClientApproval> ClientApprovals => Set<ClientApproval>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    private Guid CurrentTenantId =>
        _currentUser != null && _currentUser.TenantId.HasValue ? _currentUser.TenantId.Value : Guid.Empty;

    private bool IsSuperAdmin => _currentUser?.IsInRole(AppRoles.SuperAdmin) == true;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureTenantIsolation(builder);
        ConfigureIdentity(builder);
        ConfigureEntities(builder);
    }

    /// <summary>
    /// STRICT TENANT ISOLATION: every entity implementing ITenantEntity gets a
    /// global query filter scoping reads to the current tenant. SuperAdmin bypasses
    /// the filter (platform-wide visibility). Identity users are excluded: user
    /// scoping is enforced explicitly in UserManagementService. The filter values
    /// are referenced as properties on the context instance so EF Core
    /// parameterizes them and re-evaluates them per query (NOT baked into the
    /// cached model).
    /// </summary>
    private void ConfigureTenantIsolation(ModelBuilder builder)
    {
        var tenantEntityTypes = builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)
                        && t.ClrType != typeof(ApplicationUser));

        foreach (var entityType in tenantEntityTypes)
        {
            var param = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var tenantId = System.Linq.Expressions.Expression.Property(param, nameof(ITenantEntity.TenantId));

            var context = System.Linq.Expressions.Expression.Constant(this, GetType());
            var current = System.Linq.Expressions.Expression.Property(context, nameof(CurrentTenantId));
            var isSuperAdmin = System.Linq.Expressions.Expression.Property(context, nameof(IsSuperAdmin));

            var body = System.Linq.Expressions.Expression.OrElse(
                isSuperAdmin,
                System.Linq.Expressions.Expression.Equal(tenantId, current));
            var lambda = System.Linq.Expressions.Expression.Lambda(body, param);
            entityType.SetQueryFilter(lambda);
        }
    }

    private void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("AspNetUsers");
            e.HasIndex(u => u.TenantId);
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.MobileNumber).HasMaxLength(20);
            e.HasOne<Tenant>().WithMany(t => t.Users).HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ConfigureEntities(ModelBuilder builder)
    {
        builder.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants");
            e.Property(t => t.Id).ValueGeneratedNever();
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(80).IsRequired();
            e.Property(t => t.Email).HasMaxLength(200);
        });

        builder.Entity<TenantSetting>(e =>
        {
            e.ToTable("TenantSettings");
            e.HasIndex(s => new { s.TenantId, s.Key }).IsUnique();
            e.HasOne(s => s.Tenant).WithMany(t => t.Settings).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SubscriptionPlan>(e =>
        {
            e.ToTable("SubscriptionPlans");
            e.HasIndex(p => p.PlanName).IsUnique();
            e.Property(p => p.PlanName).HasMaxLength(100).IsRequired();
            e.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        });

        builder.Entity<Subscription>(e =>
        {
            e.ToTable("Subscriptions");
            e.HasOne(s => s.Tenant).WithMany(t => t.Subscriptions).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.TenantId);
            e.HasIndex(s => s.Status);
        });

        builder.Entity<SubscriptionTransaction>(e =>
        {
            e.ToTable("SubscriptionTransactions");
            e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TenantId);
        });

        builder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.HasOne(i => i.Subscription).WithMany().HasForeignKey(i => i.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FeatureFlag>(e => e.ToTable("FeatureFlags"));

        builder.Entity<UsageCounter>(e =>
        {
            e.ToTable("UsageCounters");
            e.HasIndex(u => u.TenantId).IsUnique();
        });

        builder.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.HasIndex(p => new { p.TenantId, p.ProjectCode }).IsUnique();
            e.Property(p => p.ProjectCode).HasMaxLength(50).IsRequired();
            e.Property(p => p.ProjectName).HasMaxLength(200).IsRequired();
            e.HasOne<Tenant>().WithMany(t => t.Projects).HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.AssignedManager).WithMany().HasForeignKey(p => p.AssignedManagerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Ipo>(e =>
        {
            e.ToTable("IPOs");
            e.HasIndex(i => new { i.TenantId, i.IpoNumber }).IsUnique();
            e.Property(i => i.IpoNumber).HasMaxLength(50).IsRequired();
            e.HasOne(i => i.Project).WithMany(p => p.Ipos).HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductCategory>(e =>
        {
            e.ToTable("ProductCategories");
            e.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
            e.Property(c => c.Name).HasMaxLength(150).IsRequired();
        });

        builder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasIndex(p => new { p.TenantId, p.ProductCode }).IsUnique();
            e.Property(p => p.ProductCode).HasMaxLength(50).IsRequired();
            e.Property(p => p.ProductName).HasMaxLength(200).IsRequired();
            e.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductProjectMapping>(e =>
        {
            e.ToTable("ProductProjectMappings");
            e.HasIndex(m => new { m.TenantId, m.ProductId, m.ProjectId }).IsUnique();
            e.HasOne(m => m.Product).WithMany(p => p.ProjectMappings).HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Project).WithMany(p => p.ProductMappings).HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SiteQuery>(e =>
        {
            e.ToTable("Queries");
            e.HasIndex(q => new { q.TenantId, q.QueryNumber }).IsUnique();
            e.HasIndex(q => new { q.TenantId, q.IpoNumber });
            e.HasIndex(q => new { q.TenantId, q.Status });
            e.HasIndex(q => new { q.TenantId, q.RaisedByUserId });
            e.Property(q => q.QueryNumber).HasMaxLength(30).IsRequired();
            e.Property(q => q.IpoNumber).HasMaxLength(50).IsRequired();
            e.HasOne(q => q.Project).WithMany(p => p.Queries).HasForeignKey(q => q.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Ipo).WithMany(i => i.Queries).HasForeignKey(q => q.IpoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(q => q.Product).WithMany(p => p.Queries).HasForeignKey(q => q.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(q => q.RaisedByUser).WithMany(u => u.RaisedQueries).HasForeignKey(q => q.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.AssignedToManager).WithMany().HasForeignKey(q => q.AssignedToManagerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<QueryPhoto>(e =>
        {
            e.ToTable("QueryPhotos");
            e.HasOne(p => p.Query).WithMany(q => q.Photos).HasForeignKey(p => p.QueryId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.TenantId, p.QueryId });
        });

        builder.Entity<QueryComment>(e =>
        {
            e.ToTable("QueryComments");
            e.HasOne(c => c.Query).WithMany(q => q.QueryComments).HasForeignKey(c => c.QueryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => new { c.TenantId, c.QueryId });
        });

        builder.Entity<QueryStatusHistory>(e =>
        {
            e.ToTable("QueryStatusHistory");
            e.HasOne(h => h.Query).WithMany(q => q.StatusHistory).HasForeignKey(h => h.QueryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedByUser).WithMany().HasForeignKey(h => h.ChangedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(h => new { h.TenantId, h.QueryId });
        });

        builder.Entity<EmailTemplate>(e =>
        {
            e.ToTable("EmailTemplates");
            e.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
            e.Property(t => t.Name).HasMaxLength(150).IsRequired();
        });

        builder.Entity<EmailRecord>(e =>
        {
            e.ToTable("EmailRecords");
            e.HasOne(r => r.Query).WithMany().HasForeignKey(r => r.QueryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.CreatedByUser).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.TenantId, r.QueryId });
        });

        builder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.HasOne(n => n.User).WithMany(u => u.Notifications).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.TenantId, n.UserId, n.IsRead });
        });

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId });
            e.HasIndex(a => new { a.TenantId, a.Timestamp });
        });

        builder.Entity<Document>(e =>
        {
            e.ToTable("Documents");
            e.HasOne(d => d.Project).WithMany(p => p.Documents).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.Query).WithMany().HasForeignKey(d => d.QueryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.Eot).WithMany(eot => eot.Documents).HasForeignKey(d => d.EotId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.UploadedByUser).WithMany().HasForeignKey(d => d.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EotRecord>(e =>
        {
            e.ToTable("EOTRecords");
            e.HasIndex(x => new { x.TenantId, x.EotNumber }).IsUnique();
            e.Property(x => x.EotNumber).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Project).WithMany(p => p.Eots).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EotDocument>(e =>
        {
            e.ToTable("EOTDocuments");
            e.HasOne(d => d.Eot).WithMany(eot => eot.EotDocuments).HasForeignKey(d => d.EotId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => new { d.TenantId, d.EotId });
        });

        builder.Entity<EotStatusHistory>(e =>
        {
            e.ToTable("EOTStatusHistory");
            e.HasOne(h => h.Eot).WithMany(eot => eot.StatusHistory).HasForeignKey(h => h.EotId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedByUser).WithMany().HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ScopeVariation>(e =>
        {
            e.ToTable("ScopeVariations");
            e.HasOne(v => v.Eot).WithMany(eot => eot.ScopeVariations).HasForeignKey(v => v.EotId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClientApproval>(e =>
        {
            e.ToTable("ClientApprovals");
            e.HasOne(c => c.Eot).WithMany(eot => eot.ClientApprovals).HasForeignKey(c => c.EotId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();

    private void ApplyAuditing()
    {
        var now = DateTime.UtcNow;
        var tenantId = _currentUser?.TenantId;
        var userName = _currentUser?.UserName;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is ITenantEntity tenantEntity && entry.State == EntityState.Added)
            {
                if (tenantEntity.TenantId == Guid.Empty)
                {
                    if (tenantId.HasValue)
                        tenantEntity.TenantId = tenantId.Value;
                    else if (entry.Entity is not ApplicationUser)
                        throw new InvalidOperationException("Tenant context is missing while creating a tenant-scoped record.");
                }
            }

            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    baseEntity.CreatedAt = now;
                    baseEntity.CreatedBy ??= userName;
                }
                if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedAt = now;
                    baseEntity.UpdatedBy = userName;
                }
            }
        }
    }
}
