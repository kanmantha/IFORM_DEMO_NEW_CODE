using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Contracts;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Infrastructure.Authentication;
using SiteQueryDefectTracking.Infrastructure.Identity;
using SiteQueryDefectTracking.Infrastructure.Persistence;
using SiteQueryDefectTracking.Infrastructure.Services;

namespace SiteQueryDefectTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection();

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IClock>(_ => new SystemClock());
        services.AddScoped<IDelayCalculator, DelayCalculator>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddScoped<IDateTimeService>(sp =>
            new AppDateTimeService(sp.GetRequiredService<IClock>()));
        services.AddScoped<ICurrentUserService, AnonymousCurrentUserService>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value);
        services.AddSingleton<TokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDomainEventPublisher, NoOpDomainEventPublisher>();

        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.AddScoped<IFileStorageService>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;
            return options.Provider?.ToLowerInvariant() == "azureblob"
                ? new AzureBlobStorageService(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureBlobStorageService>>())
                : new LocalFileStorageService(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalFileStorageService>>());
        });

        services.Configure<Application.Services.EmailOptions>(configuration.GetSection("Email"));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}

/// <summary>
/// Default event publisher (resolves without a hub). Hosts override this with a
/// SignalR / Blazor-aware implementation which wins the DI resolution order.
/// </summary>
public class NoOpDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishQueryCreatedAsync(QuerySummaryDto summary, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishQueryUpdatedAsync(QuerySummaryDto summary, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishQueryStatusChangedAsync(QuerySummaryDto summary, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishQueryResolvedAsync(QuerySummaryDto summary, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishCommentAddedAsync(Guid queryId, CommentDto comment, CancellationToken ct = default) => Task.CompletedTask;
}

public class ApplicationOptions
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string WebBaseUrl { get; set; } = string.Empty;
    public string? TimeZoneId { get; set; }
}