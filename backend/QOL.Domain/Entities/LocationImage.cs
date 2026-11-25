namespace QOL.Domain.Entities;

public sealed class LocationImage
{
    private LocationImage()
    {
    }

    public LocationImage(Guid locationId, string imageUrl, string? altText, int displayOrder)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        }

        if (string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Length > 2048)
        {
            throw new ArgumentException("Image URL must be between 1 and 2048 characters.", nameof(imageUrl));
        }

        if (altText?.Length > 500)
        {
            throw new ArgumentException("Image alt text cannot exceed 500 characters.", nameof(altText));
        }

        Id = Guid.NewGuid();
        LocationId = locationId;
        ImageUrl = imageUrl.Trim();
        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        DisplayOrder = displayOrder;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid LocationId { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string? AltText { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Location Location { get; private set; } = null!;
}
