using IForm.Application.Services;

namespace IForm.Web.Background;

/// <summary>
/// Periodic job that moves trial subscriptions into grace period, expires grace
/// periods, and refreshes tenant usage counters. Runs every 6 hours.
/// </summary>
public class SubscriptionExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public SubscriptionExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the app a moment to finish seeding on first boot.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                await subscriptionService.ProcessExpirationsAsync(stoppingToken);
                await subscriptionService.RefreshAllUsageAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription background job failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
