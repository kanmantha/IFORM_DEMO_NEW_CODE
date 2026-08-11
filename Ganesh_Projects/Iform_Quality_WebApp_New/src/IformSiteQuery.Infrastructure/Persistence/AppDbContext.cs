using IformSiteQuery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IformSiteQuery.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SiteQuery> Queries => Set<SiteQuery>();
    public DbSet<QueryComment> QueryComments => Set<QueryComment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailRecord> Emails => Set<EmailRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100);
            e.HasIndex(x => x.Code);
            e.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<SiteQuery>(e =>
        {
            e.Property(x => x.QueryNumber).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.QueryNumber).IsUnique();
            e.Property(x => x.IpoNumber).HasMaxLength(50).IsRequired();
            e.Property(x => x.PhotoPath).HasMaxLength(500);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.RaisedBy).WithMany().HasForeignKey(x => x.RaisedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.IpoNumber);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<QueryComment>(e =>
        {
            e.HasOne(x => x.Query).WithMany().HasForeignKey(x => x.QueryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Text).HasMaxLength(2000);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailRecord>(e =>
        {
            e.HasOne(x => x.Query).WithMany().HasForeignKey(x => x.QueryId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}
