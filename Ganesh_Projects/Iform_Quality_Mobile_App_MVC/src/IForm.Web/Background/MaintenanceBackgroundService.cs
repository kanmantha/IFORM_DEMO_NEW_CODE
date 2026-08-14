using IForm.Application.Services;

namespace IForm.Web.Background;

/// <summary>
/// Periodic job that escalates overdue open queries to the configured role and
/// purges photos older than each tenant's retention period. Runs every 6 hours.
/// </summary>
public class MaintenanceBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public MaintenanceBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MaintenanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the app a moment to finish seeding on first boot.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var escalation = scope.ServiceProvider.GetRequiredService<IEscalationService>();
                var escalated = await escalation.ProcessEscalationsAsync(stoppingToken);
                if (escalated > 0)
                    _logger.LogInformation("Escalated {Count} overdue queries.", escalated);

                var retention = scope.ServiceProvider.GetRequiredService<IPhotoRetentionService>();
                var purged = await retention.PurgeExpiredAsync(stoppingToken);
                if (purged > 0)
                    _logger.LogInformation("Photo retention purge removed {Count} photos.", purged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Maintenance background job failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
