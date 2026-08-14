using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Persistence;
using IForm.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace IForm.SecurityTests;

/// <summary>
/// FilesController must only stream files that are referenced by a record in the
/// caller's tenant. A bare authenticated caller must NOT be able to read arbitrary
/// storage paths (including another tenant's files).
/// </summary>
public class FilesControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _seed;

    public FilesControllerTests()
    {
        (_seed, _connection) = TestDb.Create();
    }

    public void Dispose()
    {
        _seed.Dispose();
        _connection.Dispose();
    }

    private static FilesController BuildController(TestCurrentUser user, IApplicationDbContext db)
        => new FilesController(new StubStorage(), db);

    private async Task<AppDbContext> SeedPhotoAsync(Guid tenantId, string path)
    {
        var db = TestDb.For(new TestCurrentUser { TenantId = tenantId }, _connection);
        var project = new Project { TenantId = tenantId, ProjectCode = "PRJ-1", ProjectName = "Project 1" };
        var query = new SiteQuery
        {
            TenantId = tenantId,
            QueryNumber = "SQ-0001",
            IpoNumber = "IPO-1",
            ProjectId = project.Id,
            IssueType = IssueType.Missing,
            RaisedByUserId = Guid.NewGuid()
        };
        db.Projects.Add(project);
        db.Queries.Add(query);
        db.QueryPhotos.Add(new QueryPhoto
        {
            TenantId = tenantId,
            QueryId = query.Id,
            FilePath = path,
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 10,
            UploadedByUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Download_unreferenced_path_is_not_found()
    {
        var tenant = Guid.NewGuid();
        await using var db = TestDb.For(new TestCurrentUser { TenantId = tenant }, _connection);
        var controller = BuildController(new TestCurrentUser { TenantId = tenant }, db);

        var result = await controller.Download("queries/not_referenced.jpg", CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Download_empty_or_blank_path_is_bad_request()
    {
        var tenant = Guid.NewGuid();
        await using var db = TestDb.For(new TestCurrentUser { TenantId = tenant }, _connection);
        var controller = BuildController(new TestCurrentUser { TenantId = tenant }, db);

        (await controller.Download("", CancellationToken.None)).ShouldBeOfType<BadRequestResult>();
        (await controller.Download("   ", CancellationToken.None)).ShouldBeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task Download_tenant_owned_photo_streams_file()
    {
        var tenant = Guid.NewGuid();
        var path = "queries/owned_photo.jpg";
        var seed = await SeedPhotoAsync(tenant, path);
        await seed.DisposeAsync();

        await using var db = TestDb.For(new TestCurrentUser { TenantId = tenant }, _connection);
        var controller = BuildController(new TestCurrentUser { TenantId = tenant }, db);

        var result = await controller.Download(path, CancellationToken.None);

        var file = result.ShouldBeOfType<FileStreamResult>();
        file.FileDownloadName.ShouldBe("owned_photo.jpg");
        file.ContentType.ShouldBe("image/jpeg");
    }

    [Fact]
    public async Task Download_other_tenants_photo_is_not_found()
    {
        var ownerTenant = Guid.NewGuid();
        var attackerTenant = Guid.NewGuid();
        var path = "queries/victim_photo.jpg";
        var seed = await SeedPhotoAsync(ownerTenant, path);
        await seed.DisposeAsync();

        await using var db = TestDb.For(new TestCurrentUser { TenantId = attackerTenant }, _connection);
        var controller = BuildController(new TestCurrentUser { TenantId = attackerTenant }, db);

        var result = await controller.Download(path, CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Download_works_for_super_admin()
    {
        var ownerTenant = Guid.NewGuid();
        var path = "queries/sa_photo.jpg";
        var seed = await SeedPhotoAsync(ownerTenant, path);
        await seed.DisposeAsync();

        var superAdmin = new TestCurrentUser { TenantId = Guid.Empty, Roles = new[] { "SuperAdmin" } };
        await using var db = TestDb.For(superAdmin, _connection);
        var controller = BuildController(superAdmin, db);

        var result = await controller.Download(path, CancellationToken.None);

        result.ShouldBeOfType<FileStreamResult>();
    }

    private sealed class StubStorage : IFileStorageService
    {
        public Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
            => Task.FromResult(new StoredFile("x", fileName, contentType, 0));

        public Task<StoredFile> SaveBytesAsync(byte[] content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
            => Task.FromResult(new StoredFile("x", fileName, contentType, content.Length));

        public Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
            => Task.FromResult<Stream?>(new MemoryStream(new byte[] { 1, 2, 3 }));

        public Task<bool> DeleteAsync(string path, CancellationToken ct = default) => Task.FromResult(true);

        public Task<long> GetStorageUsedBytesAsync(CancellationToken ct = default) => Task.FromResult(0L);

        public Task<byte[]> NormalizeImageAsync(Stream content, int maxWidth = 1600, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());
    }
}
