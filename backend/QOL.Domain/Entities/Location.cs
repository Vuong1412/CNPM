namespace QOL.Domain.Entities;

public enum LocationStatus
{
    Active,
    Inactive,
    Pending,
    Rejected
}

public sealed class Location
{
    private Location()
    {
    }

    public Location(
        string name,
        string address,
        decimal? latitude = null,
        decimal? longitude = null,
        int radiusMeters = 100,
        int priority = 0,
        string? originalDescription = null,
        string? directionLink = null,
        LocationStatus status = LocationStatus.Active)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        UpdateDetails(
            name,
            address,
            latitude,
            longitude,
            radiusMeters,
            priority,
            originalDescription,
            directionLink,
            status);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int RadiusMeters { get; private set; }
    public int Priority { get; private set; }
    public string? OriginalDescription { get; private set; }
    public string? DirectionLink { get; private set; }
    public LocationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<Favorite> Favorites { get; private set; } = new List<Favorite>();
    public ICollection<ListeningHistory> ListeningHistory { get; private set; } = new List<ListeningHistory>();
    public ICollection<AudioFile> AudioFiles { get; private set; } = new List<AudioFile>();
    public ICollection<LocationImage> Images { get; private set; } = new List<LocationImage>();

    public void UpdateDetails(string name, string address) =>
        UpdateDetails(
            name,
            address,
            Latitude,
            Longitude,
            RadiusMeters,
            Priority,
            OriginalDescription,
            DirectionLink,
            Status);

    public void UpdateDetails(
        string name,
        string address,
        decimal? latitude,
        decimal? longitude,
        int radiusMeters,
        int priority,
        string? originalDescription,
        string? directionLink,
        LocationStatus status)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new ArgumentException("Location name must be between 1 and 200 characters.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address) || address.Length > 500)
        {
            throw new ArgumentException("Address must be between 1 and 500 characters.", nameof(address));
        }

        ValidateCoordinates(latitude, longitude);
        if (radiusMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), "Radius must be greater than zero.");
        }

        if (originalDescription?.Length > 4000)
        {
            throw new ArgumentException("Original description cannot exceed 4000 characters.", nameof(originalDescription));
        }

        if (directionLink?.Length > 2048)
        {
            throw new ArgumentException("Direction link cannot exceed 2048 characters.", nameof(directionLink));
        }

        if (directionLink is not null &&
            (!Uri.TryCreate(directionLink, UriKind.Absolute, out var directionUri) ||
             directionUri.Scheme is not ("http" or "https")))
        {
            throw new ArgumentException("Direction link must be an absolute HTTP or HTTPS URL.", nameof(directionLink));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Name = name.Trim();
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        Priority = priority;
        OriginalDescription = string.IsNullOrWhiteSpace(originalDescription)
            ? null
            : originalDescription.Trim();
        DirectionLink = string.IsNullOrWhiteSpace(directionLink) ? null : directionLink.Trim();
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(LocationStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate() => SetStatus(LocationStatus.Inactive);

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new ArgumentException("Latitude and longitude must both be provided or both be empty.");
        }

        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        }
    }
}
