using FluentValidation;
using IForm.Application.Common.Interfaces;
using IForm.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IForm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        services.AddSingleton<IValidatorService, DefaultValidatorService>();

        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IIpoService, IpoService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IEotService, EotService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IEscalationService, EscalationService>();
        services.AddScoped<IPhotoRetentionService, PhotoRetentionService>();

        return services;
    }
}
