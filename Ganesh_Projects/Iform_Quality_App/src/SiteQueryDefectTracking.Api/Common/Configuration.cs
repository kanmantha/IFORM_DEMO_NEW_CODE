using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.IdentityModel.Tokens;
using SiteQueryDefectTracking.Api.Hubs;
using SiteQueryDefectTracking.Api.Middleware;
using SiteQueryDefectTracking.Api.Services;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Common;

public static class ApiConfiguration
{
    public const string ResponseShape = "{ success, data, message }";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var jwt = jwtSection.Get<JsonWebTokenOptions>() ?? new JsonWebTokenOptions();

        services.AddHttpContextAccessor();

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            })
            .AddMvcOptions(o => o.Filters.Add<FluentValidationActionFilter>());

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<JwtSecuritySchemeDocumentTransformer>();
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSection["SecretKey"]
                            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured."))),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.RequireManager, p => p.RequireRole(AppRoles.Manager));
            options.AddPolicy(AppPolicies.CanResolveQueries, p => p.RequireRole(AppRoles.Manager));
            options.AddPolicy(AppPolicies.CanManageCatalogue, p => p.RequireRole(AppRoles.Manager));
            options.AddPolicy(AppPolicies.CanViewDashboard, p => p.RequireRole(AppRoles.Manager));
            options.AddPolicy(AppPolicies.CanManageEmails, p => p.RequireRole(AppRoles.Manager));
            options.AddPolicy(AppPolicies.CanViewAuditLogs, p => p.RequireRole(AppRoles.Manager));
        });

        services.AddSignalR();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
                if (origins.Length == 0)
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(origins);
                }
                policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            });
        });

        services.AddHealthChecks();

        services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
        services.AddScoped<IDomainEventPublisher, SignalREventPublisher>();

        return services;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseRouting();
        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<QueriesHub>("/hubs/queries");
        app.MapHealthChecks("/health");
        return app;
    }
}

internal static class AppPolicies
{
    public const string RequireManager = "RequireManager";
    public const string CanResolveQueries = "CanResolveQueries";
    public const string CanManageCatalogue = "CanManageCatalogue";
    public const string CanViewDashboard = "CanViewDashboard";
    public const string CanManageEmails = "CanManageEmails";
    public const string CanViewAuditLogs = "CanViewAuditLogs";
}

internal static class AppRoles
{
    public const string Manager = "Manager";
    public const string SiteEngineer = "Site Engineer";
}

internal class JsonWebTokenOptions
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}