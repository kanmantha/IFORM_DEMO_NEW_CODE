using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IForm.IntegrationTests;

/// <summary>
/// Drives the production DbSeeder against an in-memory SQLite database and proves
/// seeding is idempotent (running twice yields the same stable state) and produces
/// the expected platform/demo tenants and seeded users.
/// </summary>
public class DbSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public DbSeederTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Seed_runs_cleanly_and_creates_expected_data()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await DbSeeder.SeedAsync(db, users, roles);

        db.Tenants.Count().ShouldBe(2); // platform + demo
        db.Tenants.SingleOrDefault(t => t.Slug == "platform")?.Id.ShouldBe(Guid.Empty);
        db.Tenants.SingleOrDefault(t => t.Slug == "i-form-aluminium").ShouldNotBeNull();
        db.SubscriptionPlans.Count().ShouldBe(5);
        db.EmailTemplates.IgnoreQueryFilters().Count().ShouldBe(4);
        db.UsageCounters.IgnoreQueryFilters().Count().ShouldBe(1);

        (await users.FindByEmailAsync("admin@iform.example.com")).ShouldNotBeNull();
        (await users.FindByEmailAsync("superadmin@iform.example.com")).ShouldNotBeNull();
        (await roles.FindByNameAsync("TenantAdmin")).ShouldNotBeNull();
        (await roles.FindByNameAsync("SuperAdmin")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Seed_is_idempotent_when_run_twice()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await DbSeeder.SeedAsync(db, users, roles);
        }

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await DbSeeder.SeedAsync(db, users, roles);
        }

        using var verify = _provider.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        verifyDb.Tenants.Count().ShouldBe(2);
        verifyDb.SubscriptionPlans.Count().ShouldBe(5);
        verifyDb.EmailTemplates.IgnoreQueryFilters().Count().ShouldBe(4);
        verifyDb.Users.Count().ShouldBe(2); // admin + superadmin, not duplicated
    }

    [Fact]
    public async Task Seed_self_heals_stray_platform_tenant_with_wrong_id()
    {
        // Simulate a legacy database: a platform tenant created with a random id,
        // no canonical platform row, and no super admin yet.
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Name = "Legacy Platform", Slug = "platform", Status = TenantStatus.Active });
            await db.SaveChangesAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await DbSeeder.SeedAsync(db, users, roles);
        }

        using var verify = _provider.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        verifyDb.Tenants.Count(t => t.Slug == "platform").ShouldBe(1);
        verifyDb.Tenants.Single(t => t.Slug == "platform").Id.ShouldBe(Guid.Empty);
        verifyDb.Users.Count(u => u.TenantId == Guid.Empty).ShouldBe(1);
    }
}
