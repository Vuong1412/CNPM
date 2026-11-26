using QOL.Application.Payments;

namespace QOL.Infrastructure.Payments;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public string Provider => "FAKE";

    public Task<string> CreatePaymentUrlAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var separator = request.ReturnUrl.Contains('?') ? '&' : '?';
        var url = $"{request.ReturnUrl}{separator}provider=FAKE&paymentId={request.PaymentId:D}";
        return Task.FromResult(url);
    }

    public PaymentGatewayNotification VerifyNotification(
        IReadOnlyDictionary<string, string?> fields) =>
        new(false, null, null, null, null, null, null);
}
