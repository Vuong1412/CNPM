using QOL.Application.Payments;

namespace QOL.Infrastructure.Payments;

public sealed class DisabledPaymentGateway : IPaymentGateway
{
    public string Provider => "DISABLED";

    public Task<string> CreatePaymentUrlAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Online payment is not configured. Use the manual review flow.");

    public PaymentGatewayNotification VerifyNotification(
        IReadOnlyDictionary<string, string?> fields) =>
        new(false, null, null, null, null, null, null);
}
