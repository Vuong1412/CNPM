namespace QOL.Application.Payments;

public interface IPaymentGateway
{
    string Provider { get; }

    Task<string> CreatePaymentUrlAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken);

    PaymentGatewayNotification VerifyNotification(
        IReadOnlyDictionary<string, string?> fields);
}

public sealed record PaymentLinkRequest(
    Guid PaymentId,
    Guid OrderId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    string ReturnUrl,
    string IpnUrl,
    string ClientIpAddress);

public sealed record PaymentGatewayNotification(
    bool SignatureValid,
    string? ProviderTransactionId,
    long? AmountMinorUnits,
    string? Currency,
    string? ResponseCode,
    string? TransactionStatus,
    string? ProviderReference);
