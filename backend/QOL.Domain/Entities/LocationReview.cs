namespace QOL.Domain.Entities;

public sealed class LocationReview
{
    private LocationReview()
    {
    }

    public LocationReview(Guid userId, Guid locationId, int rating, string? comment)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        LocationId = locationId;
        CreatedAt = DateTimeOffset.UtcNow;
        IsVisible = true;
        UpdateContent(rating, comment);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid LocationId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsVisible { get; private set; }
    public User User { get; private set; } = null!;
    public Location Location { get; private set; } = null!;

    public void UpdateContent(int rating, string? comment)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        if (comment?.Length > 2000)
        {
            throw new ArgumentException("Review comment cannot exceed 2000 characters.", nameof(comment));
        }

        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    public void SetVisibility(bool isVisible) => IsVisible = isVisible;
}
