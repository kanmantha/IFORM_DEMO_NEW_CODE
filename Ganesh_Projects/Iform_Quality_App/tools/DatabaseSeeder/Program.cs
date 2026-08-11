using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SiteQueryDefectTracking.Application;
using SiteQueryDefectTracking.Infrastructure;
using SiteQueryDefectTracking.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>(_ => { });
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();

try
{
    await DbSeeder.SeedAsync(host.Services);
    Console.WriteLine("Database seeded successfully.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Seeding failed: {ex.Message}");
    return 1;
}

return 0;