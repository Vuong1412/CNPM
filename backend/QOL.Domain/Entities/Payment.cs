namespace QOL.Domain.Entities;

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed
}

public sealed class Payment
{
    private Payment()
    {
    }

    public Payment(
        Guid orderId,
        string provider,
        string providerTxnId,
        decimal amount,
        string currency)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(provider) || provider.Length > 32)
        {
            throw new ArgumentException("Provider must be between 1 and 32 characters.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(providerTxnId) || providerTxnId.Length > 100)
        {
            throw new ArgumentException("Provider transaction ID must be between 1 and 100 characters.", nameof(providerTxnId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        }

        Id = Guid.NewGuid();
        OrderId = orderId;
        Provider = provider.Trim();
        ProviderTxnId = providerTxnId.Trim();
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderTxnId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public SubscriptionOrder Order { get; private set; } = null!;

    public bool MarkPaid(DateTimeOffset? now = null)
    {
        if (Status == PaymentStatus.Paid)
        {
            return false;
        }

        Status = PaymentStatus.Paid;
        PaidAt = now ?? DateTimeOffset.UtcNow;
        return true;
    }

    public bool MarkFailed()
    {
        if (Status != PaymentStatus.Pending)
        {
            return false;
        }

        Status = PaymentStatus.Failed;
        return true;
    }
}
