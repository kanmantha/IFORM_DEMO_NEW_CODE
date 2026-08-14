using IForm.Contracts;
using QRCoder;
using Microsoft.Extensions.Logging;

namespace IForm.Infrastructure.Services;

public class QrCodeService : IQrCodeService
{
    private readonly ILogger<QrCodeService> _logger;

    public QrCodeService(ILogger<QrCodeService> logger) => _logger = logger;

    public byte[] GeneratePng(string content, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}

/// <summary>
/// Mock payment gateway used when a real provider's credentials are not available.
/// It records the transaction and simulates a successful payment.
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger) => _logger = logger;

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(PaymentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("MOCK PAYMENT: creating intent for {Amount} {Currency} ({Description})", request.Amount, request.Currency, request.Description);
        return Task.FromResult(new PaymentIntentResult(true, $"mock_{Guid.NewGuid():N}", "mock_client_secret", null, "requires_confirmation"));
    }

    public Task<PaymentIntentResult> ConfirmPaymentAsync(string paymentIntentId, CancellationToken ct = default)
    {
        _logger.LogInformation("MOCK PAYMENT: confirming intent {Id}", paymentIntentId);
        return Task.FromResult(new PaymentIntentResult(true, paymentIntentId, null, null, "succeeded"));
    }

    public Task<PaymentIntentResult> RefundAsync(string paymentIntentId, decimal amount, CancellationToken ct = default)
    {
        _logger.LogInformation("MOCK PAYMENT: refunding {Amount} for {Id}", amount, paymentIntentId);
        return Task.FromResult(new PaymentIntentResult(true, paymentIntentId, null, null, "refunded"));
    }
}
