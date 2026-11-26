namespace QOL.Domain.Entities;

public sealed class LocationTranslation
{
    private LocationTranslation()
    {
    }

    public LocationTranslation(Guid locationId, Guid languageId, string text)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        }

        if (languageId == Guid.Empty)
        {
            throw new ArgumentException("Language ID cannot be empty.", nameof(languageId));
        }

        Id = Guid.NewGuid();
        LocationId = locationId;
        LanguageId = languageId;
        Text = ValidateText(text);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateText(string text)
    {
        Text = ValidateText(text);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 50000)
        {
            throw new ArgumentException("Translation text must be between 1 and 50000 characters.", nameof(text));
        }

        return text.Trim();
    }

    public Guid Id { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid LanguageId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public Location Location { get; private set; } = null!;
    public Language Language { get; private set; } = null!;
    public ICollection<AudioFile> AudioFiles { get; private set; } = new List<AudioFile>();
}
