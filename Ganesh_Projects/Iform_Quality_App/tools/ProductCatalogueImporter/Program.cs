using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SiteQueryDefectTracking.Application;
using SiteQueryDefectTracking.Infrastructure;
using SiteQueryDefectTracking.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: ProductCatalogueImporter <path-to-csv-or-xlsx>");
    return 1;
}

try
{
    var scope = host.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var path = Path.GetFullPath(args[0]);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 1;
    }

    var imported = await ImportAsync(context, path);
    Console.WriteLine($"Imported {imported} product codes from {Path.GetFileName(path)}.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Import failed: {ex.Message}");
    return 1;
}

static async Task<int> ImportAsync(AppDbContext context, string path)
{
    var count = 0;
    var lines = File.ReadAllLines(path);
    for (var i = 1; i < lines.Length; i++)
    {
        var parts = lines[i].Split(',');
        if (parts.Length < 2) continue;

        var code = parts[0].Trim();
        var name = parts[1].Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;
        if (await context.ProductCodes.AnyAsync(p => p.Code == code)) continue;

        context.ProductCodes.Add(new SiteQueryDefectTracking.Domain.Entities.ProductCode
        {
            Code = code,
            Name = name,
            Description = parts.Length > 2 ? parts[2].Trim() : null,
            Specification = parts.Length > 3 ? parts[3].Trim() : null,
            Material = parts.Length > 4 ? parts[4].Trim() : null,
            IsActive = true,
            IsVerified = true
        });
        count++;
    }

    await context.SaveChangesAsync();
    return count;
}