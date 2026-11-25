namespace QOL.Domain.Entities;

public sealed class ListeningHistory
{
    private ListeningHistory()
    {
    }

    public ListeningHistory(
        Guid userId,
        Guid locationId,
        Guid audioId,
        Guid languageId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        }

        if (audioId == Guid.Empty)
        {
            throw new ArgumentException("Audio ID cannot be empty.", nameof(audioId));
        }

        if (languageId == Guid.Empty)
        {
            throw new ArgumentException("Language ID cannot be empty.", nameof(languageId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        LocationId = locationId;
        AudioId = audioId;
        LanguageId = languageId;
        ListenedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProgress(int durationSeconds, bool isCompleted)
    {
        if (durationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration cannot be negative.");
        }

        DurationSeconds = durationSeconds;
        IsCompleted = isCompleted;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid AudioId { get; private set; }
    public Guid LanguageId { get; private set; }
    public DateTimeOffset ListenedAt { get; private set; }
    public int DurationSeconds { get; private set; }
    public bool IsCompleted { get; private set; }
    public User User { get; private set; } = null!;
    public Location Location { get; private set; } = null!;
    public AudioFile AudioFile { get; private set; } = null!;
    public Language Language { get; private set; } = null!;
}
