using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using IForm.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IForm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddSingleton<IDateTime, UtcDateTime>();
        services.AddSingleton<IQrCodeService, QrCodeService>();

        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        if (string.Equals(storageProvider, "Azure", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStorageService, AzureBlobFileStorageService>();
        else
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        var emailProvider = configuration["Email:Provider"] ?? "Log";
        if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, LogOnlyEmailSender>();

        var paymentProvider = configuration["Payment:Provider"] ?? "Mock";
        if (string.Equals(paymentProvider, "Mock", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
        else
            services.AddSingleton<IPaymentGateway, MockPaymentGateway>();

        services.AddScoped<ITenantSettingsProvider, TenantSettingsProvider>();

        return services;
    }
}
