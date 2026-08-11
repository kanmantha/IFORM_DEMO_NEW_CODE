using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application;
using SiteQueryDefectTracking.Infrastructure;
using SiteQueryDefectTracking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.ConfigurePipeline();
app.Run();

public partial class Program { }