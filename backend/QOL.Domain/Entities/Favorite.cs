namespace QOL.Domain.Entities;

public sealed class Favorite
{
    private Favorite()
    {
    }

    public Favorite(Guid userId, Guid locationId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        }

        UserId = userId;
        LocationId = locationId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public Guid LocationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public User User { get; private set; } = null!;
    public Location Location { get; private set; } = null!;
}
