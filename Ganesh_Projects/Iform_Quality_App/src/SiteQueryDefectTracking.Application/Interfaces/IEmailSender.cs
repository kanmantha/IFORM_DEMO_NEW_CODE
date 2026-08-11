namespace SiteQueryDefectTracking.Application.Interfaces;

public record EmailMessage(
    string From,
    IReadOnlyList<string> To,
    string Subject,
    string Body);

/// <summary>
/// Email transport abstraction — SMTP today, Microsoft Graph / other providers later
/// (BRD: "Do not hard-code an email provider").
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}