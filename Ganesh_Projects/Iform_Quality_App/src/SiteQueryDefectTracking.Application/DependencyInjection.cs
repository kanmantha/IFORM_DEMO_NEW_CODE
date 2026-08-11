using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Application.Services;

namespace SiteQueryDefectTracking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<ICatalogueJobStore, InMemoryCatalogueJobStore>();

        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuditLogQueryService, AuditLogService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IReferenceService, ReferenceService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}