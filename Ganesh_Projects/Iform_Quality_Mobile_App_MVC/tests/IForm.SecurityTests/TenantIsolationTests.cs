using IForm.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace IForm.SecurityTests;

/// <summary>
/// The EF tenant query filter must scope every read to the caller's tenant and
/// be re-evaluated per context instance (no stale constants).
/// </summary>
public class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenantA;
    private readonly Guid _tenantB;
    private readonly AppDbContext _seed;

    public TenantIsolationTests()
    {
        (_seed, _connection, _tenantA, _tenantB) = TestDb.CreateWithData();
    }

    public void Dispose()
    {
        _seed.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Tenant_user_only_sees_own_tenant_projects()
    {
        await using var db = TestDb.For(new TestCurrentUser { TenantId = _tenantA }, _connection);

        var projects = await db.Projects.Select(p => p.ProjectName).ToListAsync();

        projects.ShouldContain("Alpha Tower");
        projects.ShouldNotContain("Beta Tower");
    }

    [Fact]
    public async Task Other_tenant_user_sees_different_data()
    {
        await using var db = TestDb.For(new TestCurrentUser { TenantId = _tenantB }, _connection);

        var projects = await db.Projects.Select(p => p.ProjectName).ToListAsync();

        projects.ShouldContain("Beta Tower");
        projects.ShouldNotContain("Alpha Tower");
    }

    [Fact]
    public async Task Super_admin_bypasses_tenant_filter()
    {
        var user = new TestCurrentUser { TenantId = Guid.Empty, Roles = new[] { "SuperAdmin" } };
        await using var db = TestDb.For(user, _connection);

        var projects = await db.Projects.Select(p => p.ProjectName).ToListAsync();

        projects.ShouldContain("Alpha Tower");
        projects.ShouldContain("Beta Tower");
    }

    [Fact]
    public async Task Platform_user_without_superadmin_role_sees_nothing()
    {
        // A user on the platform tenant (Guid.Empty) without the SuperAdmin role must
        // not silently see every tenant — only SuperAdmin grants the bypass.
        await using var db = TestDb.For(new TestCurrentUser { TenantId = Guid.Empty }, _connection);

        var projects = await db.Projects.ToListAsync();

        projects.ShouldBeEmpty();
    }
}
