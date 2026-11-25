namespace QOL.Domain.Entities;

public enum SubscriptionOrderStatus
{
    Pending,
    Verified,
    Rejected
}

public sealed class SubscriptionOrder
{
    private SubscriptionOrder()
    {
    }

    public SubscriptionOrder(Guid userId, SubscriptionPlan plan)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsActive)
        {
            throw new ArgumentException("Inactive plans cannot be ordered.", nameof(plan));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        PlanId = plan.Id;
        PlanName = plan.Name;
        Amount = plan.Price;
        Currency = plan.Currency;
        Status = SubscriptionOrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PlanId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public SubscriptionOrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewNote { get; private set; }
    public User User { get; private set; } = null!;
    public SubscriptionPlan Plan { get; private set; } = null!;

    public void Review(SubscriptionOrderStatus status, Guid adminUserId, string? note)
    {
        if (Status != SubscriptionOrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending orders can be reviewed.");
        }

        if (status is not (SubscriptionOrderStatus.Verified or SubscriptionOrderStatus.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A reviewed order must be verified or rejected.");
        }

        if (adminUserId == Guid.Empty)
        {
            throw new ArgumentException("Administrator ID cannot be empty.", nameof(adminUserId));
        }

        if (note?.Length > 1000)
        {
            throw new ArgumentException("Review note cannot exceed 1000 characters.", nameof(note));
        }

        Status = status;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewedByUserId = adminUserId;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
