namespace QOL.Domain.Entities;

public sealed class SubscriptionPlan
{
    private SubscriptionPlan()
    {
    }

    public SubscriptionPlan(
        string name,
        string description,
        decimal price,
        string currency,
        int durationDays)
    {
        Id = Guid.NewGuid();
        UpdateDetails(name, description, price, currency, durationDays);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public int DurationDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateDetails(
        string name,
        string description,
        decimal price,
        string currency,
        int durationDays)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            throw new ArgumentException("Plan name must be between 1 and 120 characters.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > 1000)
        {
            throw new ArgumentException("Plan description must be between 1 and 1000 characters.", nameof(description));
        }

        if (price < 0 || price > 1000000000)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Plan price must be between 0 and 1,000,000,000.");
        }

        var normalizedCurrency = currency?.Trim();
        if (normalizedCurrency is null ||
            normalizedCurrency.Length != 3 ||
            !normalizedCurrency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        }

        if (durationDays <= 0 || durationDays > 3650)
        {
            throw new ArgumentOutOfRangeException(nameof(durationDays), "Plan duration must be between 1 and 3650 days.");
        }

        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Currency = normalizedCurrency.ToUpperInvariant();
        DurationDays = durationDays;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
