using IForm.Application.Common.Interfaces;
using IForm.Application.Services;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Infrastructure.Services;
using IForm.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IForm.IntegrationTests;

/// <summary>
/// Exercises the periodic maintenance services (query escalation and photo
/// retention) against an in-memory SQLite database.
/// </summary>
public class MaintenanceServicesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _managerUserId = Guid.NewGuid();
    private readonly Guid _engineerUserId = Guid.NewGuid();

    public MaintenanceServicesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:Escalation:Enabled"] = "true",
                ["Features:Escalation:Days"] = "10",
                ["Features:Escalation:Role"] = "Manager",
                ["Features:Photos:RetentionMonths"] = "1",
                ["ApplicationBaseUrl"] = "http://localhost"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        _provider = services.BuildServiceProvider();
        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Maintenance Test", Slug = "maintenance-test", Status = TenantStatus.Active });
        await db.SaveChangesAsync();

        var manager = new ApplicationUser
        {
            Id = _managerUserId,
            UserName = "mgr", Email = "mgr@test.example.com", FullName = "Test Manager",
            TenantId = _tenantId, IsActive = true
        };
        var engineer = new ApplicationUser
        {
            Id = _engineerUserId,
            UserName = "eng", Email = "eng@test.example.com", FullName = "Test Engineer",
            TenantId = _tenantId, IsActive = true
        };
        db.Users.AddRange(manager, engineer);
        await db.SaveChangesAsync();

        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Manager" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _managerUserId, RoleId = role.Id });
        await db.SaveChangesAsync();

        var project = new Project
        {
            TenantId = _tenantId, ProjectCode = "PRJ-MT", ProjectName = "Maintenance Project", Status = ProjectStatus.Active
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.Queries.Add(new SiteQuery
        {
            TenantId = _tenantId,
            QueryNumber = "SQ-MT1",
            IpoNumber = "IPO-MT1",
            ProjectId = project.Id,
            IssueType = IssueType.Missing,
            Status = QueryStatus.Pending,
            RaisedDate = DateTime.UtcNow.AddDays(-20),
            RaisedByUserId = _engineerUserId
        });
        await db.SaveChangesAsync();

        var queryId = db.Queries.IgnoreQueryFilters().Where(q => q.QueryNumber == "SQ-MT1").Select(q => q.Id).Single();
        db.QueryPhotos.Add(new QueryPhoto
        {
            TenantId = _tenantId,
            QueryId = queryId,
            FilePath = "queries/old-photo.jpg",
            FileName = "old-photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            UploadedAt = DateTime.UtcNow.AddMonths(-3),
            UploadedByUserId = _engineerUserId
        });
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private (IEscalationService Escalation, IPhotoRetentionService Retention, AppDbContext Db, RecordingStorage Storage) Services()
    {
        var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = new TenantSettingsProvider(db, _provider.GetRequiredService<IConfiguration>());
        var notifications = new NotificationService(db);
        var storage = new RecordingStorage();

        var escalation = new EscalationService(db, settings, notifications, NullLogger<EscalationService>.Instance);
        var retention = new PhotoRetentionService(db, settings, storage, NullLogger<PhotoRetentionService>.Instance);
        return (escalation, retention, db, storage);
    }

    [Fact]
    public async Task Escalation_notifies_manager_once_for_overdue_query()
    {
        var (escalation, _, db, _) = Services();

        var first = await escalation.ProcessEscalationsAsync();
        first.ShouldBe(1);

        var notifications = await db.Notifications.IgnoreQueryFilters()
            .Where(n => n.TenantId == _tenantId && n.Type == NotificationType.CriticalDelay)
            .ToListAsync();
        notifications.Count.ShouldBe(1);
        notifications[0].UserId.ShouldBe(_managerUserId);
        notifications[0].Message.ShouldContain("SQ-MT1");

        // Idempotent: the same query must not be escalated a second time.
        var second = await escalation.ProcessEscalationsAsync();
        second.ShouldBe(0);
        var count = await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.TenantId == _tenantId && n.Type == NotificationType.CriticalDelay);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Escalation_targets_only_the_configured_role_user()
    {
        var (escalation, _, db, _) = Services();

        var first = await escalation.ProcessEscalationsAsync();
        first.ShouldBe(1);

        var recipients = await db.Notifications.IgnoreQueryFilters()
            .Where(n => n.TenantId == _tenantId && n.Type == NotificationType.CriticalDelay)
            .Select(n => n.UserId)
            .ToListAsync();
        recipients.ShouldBe(new List<Guid?> { _managerUserId });
        recipients.ShouldNotContain(_engineerUserId);
    }

    [Fact]
    public async Task Escalation_skips_queries_below_the_threshold()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var projectId = await db.Projects.IgnoreQueryFilters().Where(p => p.TenantId == _tenantId).Select(p => p.Id).SingleAsync();
        db.Queries.Add(new SiteQuery
        {
            TenantId = _tenantId,
            QueryNumber = "SQ-MT2",
            IpoNumber = "IPO-MT2",
            ProjectId = projectId,
            IssueType = IssueType.Missing,
            Status = QueryStatus.Pending,
            RaisedDate = DateTime.UtcNow.AddDays(-2),
            RaisedByUserId = _engineerUserId
        });
        await db.SaveChangesAsync();

        var (escalation, _, _, _) = Services();
        var escalated = await escalation.ProcessEscalationsAsync();

        // Only SQ-MT1 (20 days) is over the 10-day threshold; SQ-MT2 (2 days) is not.
        escalated.ShouldBe(1);
    }

    [Fact]
    public async Task Photo_retention_purges_expired_photos_and_deletes_files()
    {
        var (_, retention, db, storage) = Services();

        var removed = await retention.PurgeExpiredAsync();
        removed.ShouldBe(1);

        var remaining = await db.QueryPhotos.IgnoreQueryFilters().CountAsync(p => p.TenantId == _tenantId);
        remaining.ShouldBe(0);
        storage.Deleted.ShouldBe(new[] { "queries/old-photo.jpg" });
    }

    private sealed class RecordingStorage : IFileStorageService
    {
        public List<string> Deleted { get; } = new();

        public Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<StoredFile> SaveBytesAsync(byte[] content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Stream?> OpenAsync(string path, CancellationToken ct = default) => Task.FromResult<Stream?>(null);

        public Task<bool> DeleteAsync(string path, CancellationToken ct = default)
        {
            Deleted.Add(path);
            return Task.FromResult(true);
        }

        public Task<long> GetStorageUsedBytesAsync(CancellationToken ct = default) => Task.FromResult(0L);

        public Task<byte[]> NormalizeImageAsync(Stream content, int maxWidth = 1600, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
