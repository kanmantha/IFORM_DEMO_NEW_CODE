namespace IForm.Contracts;

/// <summary>Provides the current signed-in user and their tenant context.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? UserName { get; }
    string? FullName { get; }
    IEnumerable<string> Roles { get; }
    bool IsInRole(string role);
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface IDateTime
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default);
    Task<StoredFile> SaveBytesAsync(byte[] content, string fileName, string contentType, string? folder = null, CancellationToken ct = default);
    Task<Stream?> OpenAsync(string path, CancellationToken ct = default);
    Task<bool> DeleteAsync(string path, CancellationToken ct = default);
    Task<long> GetStorageUsedBytesAsync(CancellationToken ct = default);
    /// <summary>Transforms an image (resize + re-encode to JPEG) if supported.</summary>
    Task<byte[]> NormalizeImageAsync(Stream content, int maxWidth = 1600, CancellationToken ct = default);
}

public sealed record StoredFile(string Path, string FileName, string ContentType, long SizeBytes, string? Url = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    string? Cc = null,
    string? Bcc = null,
    bool IsHtml = true,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);

public interface IQrCodeService
{
    byte[] GeneratePng(string content, int pixelsPerModule = 8);
}

/// <summary>Abstraction over a payment provider. Mock implementation used when credentials are unavailable.</summary>
public interface IPaymentGateway
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(PaymentRequest request, CancellationToken ct = default);
    Task<PaymentIntentResult> ConfirmPaymentAsync(string paymentIntentId, CancellationToken ct = default);
    Task<PaymentIntentResult> RefundAsync(string paymentIntentId, decimal amount, CancellationToken ct = default);
}

public sealed record PaymentRequest(decimal Amount, string Currency, string Description, string? CustomerId = null, string? ReturnUrl = null);
public sealed record PaymentIntentResult(bool Success, string? PaymentIntentId, string? ClientSecret, string? Error, string? Status = null);
