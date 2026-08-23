using IForm.Application;
using IForm.Application.Common.Interfaces;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Infrastructure;
using IForm.Persistence;
using IForm.Web;
using IForm.Web.Background;
using IForm.Web.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine("logs", "app-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();
    builder.Services.AddHttpContextAccessor();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddPersistence(builder.Configuration);

    builder.Services
        .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddClaimsPrincipalFactory<ApplicationClaimsFactory>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("TenantUsers", p => p.RequireRole(AppRoles.TenantAdmin, AppRoles.Manager, AppRoles.SiteEngineer));
        options.AddPolicy("TenantAdmin", p => p.RequireRole(AppRoles.TenantAdmin, AppRoles.SuperAdmin));
        options.AddPolicy("ManagerOnly", p => p.RequireRole(AppRoles.TenantAdmin, AppRoles.Manager));
        options.AddPolicy("SuperAdminOnly", p => p.RequireRole(AppRoles.SuperAdmin));
    });

    builder.Services.AddSwaggerGen();

    builder.Services.AddHealthChecks();

    builder.Services.AddHostedService<SubscriptionExpiryBackgroundService>();
    builder.Services.AddHostedService<MaintenanceBackgroundService>();

    var app = builder.Build();

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    app.UseAuthentication();
    app.UseMiddleware<TenantContextMiddleware>();
    app.UseAuthorization();

    app.MapStaticAssets();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();
    app.MapRazorPages();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/live");

    await SeedDatabaseAsync(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        await DbSeeder.SeedAsync(context, userManager, roleManager,
            configuration["Seed:SuperAdminEmail"],
            configuration["Seed:SuperAdminPassword"]);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the database.");
        throw;
    }
}

public partial class Program { }
