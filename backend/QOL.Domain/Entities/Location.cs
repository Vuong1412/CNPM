namespace QOL.Domain.Entities;

public sealed class Location
{
    private Location()
    {
    }

    public Location(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new ArgumentException("Location name must be between 1 and 200 characters.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address) || address.Length > 500)
        {
            throw new ArgumentException("Address must be between 1 and 500 characters.", nameof(address));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Address = address.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public ICollection<Favorite> Favorites { get; private set; } = new List<Favorite>();
    public ICollection<ListeningHistory> ListeningHistory { get; private set; } = new List<ListeningHistory>();
    public ICollection<AudioFile> AudioFiles { get; private set; } = new List<AudioFile>();

    public void UpdateDetails(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new ArgumentException("Location name must be between 1 and 200 characters.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address) || address.Length > 500)
        {
            throw new ArgumentException("Address must be between 1 and 500 characters.", nameof(address));
        }

        Name = name.Trim();
        Address = address.Trim();
    }

    public void Deactivate() => IsActive = false;
}
