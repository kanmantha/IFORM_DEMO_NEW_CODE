using IForm.Contracts;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace IForm.Infrastructure.Services;

/// <summary>SMTP email sender backed by MailKit.</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var host = _configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SMTP host is not configured. Email was not delivered. Subject: {Subject}", message.Subject);
            return;
        }

        var port = _configuration.GetValue<int>("Email:Smtp:Port", 587);
        var useSsl = _configuration.GetValue<bool>("Email:Smtp:UseSsl", false);
        var userName = _configuration["Email:Smtp:UserName"];
        var password = _configuration["Email:Smtp:Password"];
        var from = _configuration["Email:From"] ?? "no-reply@iform.example.com";
        var fromName = _configuration["Email:FromName"] ?? "I-FORM Site Query";

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(fromName, from));
        foreach (var recipient in Split(message.To)) mime.To.Add(MailboxAddress.Parse(recipient));
        foreach (var recipient in Split(message.Cc)) mime.Cc.Add(MailboxAddress.Parse(recipient));
        foreach (var recipient in Split(message.Bcc)) mime.Bcc.Add(MailboxAddress.Parse(recipient));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder();
        if (message.IsHtml) builder.HtmlBody = message.Body;
        else builder.TextBody = message.Body;
        if (message.Attachments != null)
        {
            foreach (var attachment in message.Attachments)
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }
        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrWhiteSpace(userName))
            await client.AuthenticateAsync(userName, password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent to {To} subject {Subject}", message.To, message.Subject);
    }

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
}

/// <summary>Development sender that logs the email instead of delivering it.
/// Used when no SMTP credentials are configured.</summary>
public class LogOnlyEmailSender : IEmailSender
{
    private readonly ILogger<LogOnlyEmailSender> _logger;

    public LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("EMAIL (dev, not delivered) -> To: {To}, Subject: {Subject}, Body: {Body}",
            message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
