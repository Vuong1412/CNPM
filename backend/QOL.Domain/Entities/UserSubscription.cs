namespace QOL.Domain.Entities;

public sealed class UserSubscription
{
    private UserSubscription()
    {
    }

    public UserSubscription(
        Guid userId,
        Guid planId,
        Guid orderId,
        string planName,
        string currency,
        decimal amount,
        int durationDays)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (planId == Guid.Empty) throw new ArgumentException("Plan ID cannot be empty.", nameof(planId));
        if (orderId == Guid.Empty) throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        if (durationDays <= 0) throw new ArgumentOutOfRangeException(nameof(durationDays));

        Id = Guid.NewGuid();
        UserId = userId;
        PlanId = planId;
        OrderId = orderId;
        PlanName = planName.Trim();
        Currency = currency.Trim().ToUpperInvariant();
        Amount = amount;
        StartsAt = DateTimeOffset.UtcNow;
        EndsAt = StartsAt.AddDays(durationDays);
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid OrderId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool IsActive { get; private set; }
    public User User { get; private set; } = null!;
    public SubscriptionPlan Plan { get; private set; } = null!;
    public SubscriptionOrder Order { get; private set; } = null!;

    public void ExpireIfNeeded(DateTimeOffset now)
    {
        if (IsActive && now >= EndsAt)
        {
            IsActive = false;
        }
    }
}
