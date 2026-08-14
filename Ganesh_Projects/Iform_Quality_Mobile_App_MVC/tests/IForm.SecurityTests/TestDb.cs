using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Persistence;

namespace IForm.SecurityTests;

/// <summary>Creates an in-memory SQLite-backed AppDbContext for security tests.</summary>
public static class TestDb
{
    public static (AppDbContext Db, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options, currentUser: null);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    public static AppDbContext For(TestCurrentUser user, SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options, currentUser: user);
    }

    public static (AppDbContext Seed, SqliteConnection Connection, Guid TenantA, Guid TenantB) CreateWithData()
    {
        var (db, connection) = Create();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantA, Name = "Tenant A", Slug = "tenant-a", Status = TenantStatus.Active });
        db.Tenants.Add(new Tenant { Id = tenantB, Name = "Tenant B", Slug = "tenant-b", Status = TenantStatus.Active });
        db.Projects.Add(new Project { TenantId = tenantA, ProjectCode = "PRJ-A", ProjectName = "Alpha Tower" });
        db.Projects.Add(new Project { TenantId = tenantB, ProjectCode = "PRJ-B", ProjectName = "Beta Tower" });
        db.SaveChanges();
        return (db, connection, tenantA, tenantB);
    }
}
