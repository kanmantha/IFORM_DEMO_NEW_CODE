using FluentValidation;

namespace IForm.Application.Common.Interfaces;

/// <summary>Cross-cutting services that the application layer depends on.</summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyAsync(
        IForm.Domain.Enums.NotificationType type,
        string title,
        string message,
        Guid? userId = null,
        string? link = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<IForm.Domain.Entities.Notification>> GetForCurrentUserAsync(Guid tenantId, Guid userId, int take = 50, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid id, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

public interface ITenantSettingsProvider
{
    TenantFeatureConfig GetFeatures(Guid tenantId);
    SeverityThresholdConfig GetSeverityThresholds(Guid tenantId);
}

public sealed record TenantFeatureConfig(
    bool EscalationEnabled = false,
    int EscalationDays = 10,
    string EscalationRole = "TenantAdmin",
    string CatalogueOwner = "TenantAdmin",
    int PhotoRetentionMonths = 0,
    string BaseUrl = "");

public sealed record SeverityThresholdConfig(int Watch = 7, int Delayed = 15, int Critical = 30, int Severe = 45);

public interface IValidatorService
{
    Task<IReadOnlyList<string>> ValidateAsync<T>(T instance, CancellationToken ct = default) where T : class;
}

public class DefaultValidatorService : IValidatorService
{
    private readonly IEnumerable<IValidator> _validators;

    public DefaultValidatorService(IEnumerable<IValidator> validators) => _validators = validators;

    public async Task<IReadOnlyList<string>> ValidateAsync<T>(T instance, CancellationToken ct = default) where T : class
    {
        var errors = new List<string>();
        foreach (var validator in _validators.OfType<IValidator<T>>())
        {
            var result = await validator.ValidateAsync(instance, ct);
            errors.AddRange(result.Errors.Select(e => e.ErrorMessage));
        }
        return errors;
    }
}
