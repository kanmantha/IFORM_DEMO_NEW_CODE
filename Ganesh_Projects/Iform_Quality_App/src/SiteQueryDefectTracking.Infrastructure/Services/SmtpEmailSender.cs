using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Application.Services;

namespace SiteQueryDefectTracking.Infrastructure.Services;

/// <summary>
/// SMTP email transport. The provider is pluggable — Microsoft Graph can be
/// introduced later behind the same IEmailSender contract (BRD requires no
/// hard-coded provider).
/// </summary>
public class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.Host) &&
        !string.IsNullOrWhiteSpace(options.Value.FromAddress);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("SMTP is not configured. Email to {To} was not actually sent.", string.Join(";", message.To));
            return;
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Value.Host!, options.Value.Port, options.Value.UseSsl, cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.Value.UserName))
        {
            await client.AuthenticateAsync(options.Value.UserName, options.Value.Password ?? string.Empty, cancellationToken);
        }

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(message.From));
        foreach (var to in message.To)
        {
            mime.To.Add(MailboxAddress.Parse(to));
        }

        mime.Subject = message.Subject;
        var bodyBuilder = new BodyBuilder
        {
            TextBody = message.Body,
            HtmlBody = SimpleHtml(message.Body)
        };
        mime.Body = bodyBuilder.ToMessageBody();

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email sent to {To} with subject {Subject}.", string.Join(";", message.To), message.Subject);
    }

    private static string SimpleHtml(string text)
    {
        var escaped = System.Net.WebUtility.HtmlEncode(text);
        return $"<pre style=\"font-family:Segoe UI,Arial;white-space:pre-wrap;\">{escaped}</pre>";
    }
}