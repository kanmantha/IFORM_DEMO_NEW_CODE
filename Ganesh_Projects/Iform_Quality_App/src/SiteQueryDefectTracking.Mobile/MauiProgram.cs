using Microsoft.Extensions.Logging;
using SiteQueryDefectTracking.Mobile.Pages;
using SiteQueryDefectTracking.Mobile.Services;

namespace SiteQueryDefectTracking.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton(new HttpClient());
		builder.Services.AddSingleton<ApiClient>();
		builder.Services.AddSingleton<ProjectService>();
		builder.Services.AddSingleton<ReferenceService>();
		builder.Services.AddSingleton<QueryService>();
		builder.Services.AddSingleton<DashboardService>();
		builder.Services.AddSingleton<ProductService>();
		builder.Services.AddSingleton<EmailService>();
		builder.Services.AddSingleton<NotificationService>();

		builder.Services.AddSingleton<LoginPage>();
		builder.Services.AddSingleton<DashboardPage>();
		builder.Services.AddSingleton<ReportPage>();
		builder.Services.AddSingleton<SearchPage>();
		builder.Services.AddSingleton<ProductsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}