namespace QOL.Domain.Entities;

public enum AudioSource
{
    Upload,
    Tts
}

public sealed class AudioFile
{
    private AudioFile()
    {
    }

    public AudioFile(
        Guid locationId,
        Guid languageId,
        string audioUrl,
        int durationSeconds,
        AudioSource source = AudioSource.Upload,
        Guid? translationId = null)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));
        }

        if (languageId == Guid.Empty)
        {
            throw new ArgumentException("Language ID cannot be empty.", nameof(languageId));
        }

        if (string.IsNullOrWhiteSpace(audioUrl) || audioUrl.Length > 2048)
        {
            throw new ArgumentException("Audio URL must be between 1 and 2048 characters.", nameof(audioUrl));
        }

        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Audio duration must be positive.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }
        if ((source == AudioSource.Tts) != (translationId is not null))
        {
            throw new ArgumentException(
                "TTS audio must reference a translation and uploaded audio must not.",
                nameof(translationId));
        }

        Id = Guid.NewGuid();
        LocationId = locationId;
        LanguageId = languageId;
        Title = "Audio guide";
        Transcript = string.Empty;
        AudioUrl = audioUrl.Trim();
        DurationSeconds = durationSeconds;
        IsActive = true;
        Source = source;
        TranslationId = translationId;
    }

    public AudioFile(
        Guid locationId,
        Guid languageId,
        string title,
        string transcript,
        string audioUrl,
        int durationSeconds,
        AudioSource source = AudioSource.Upload,
        Guid? translationId = null)
        : this(locationId, languageId, audioUrl, durationSeconds, source, translationId)
    {
        UpdateContent(title, transcript);
    }

    public void UpdateContent(string title, string transcript)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new ArgumentException("Audio title must be between 1 and 200 characters.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(transcript) || transcript.Length > 50000)
        {
            throw new ArgumentException("Transcript must be between 1 and 50000 characters.", nameof(transcript));
        }

        Title = title.Trim();
        Transcript = transcript.Trim();
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void ReplaceFile(
        string audioUrl,
        int durationSeconds,
        AudioSource source = AudioSource.Upload,
        Guid? translationId = null)
    {
        if (string.IsNullOrWhiteSpace(audioUrl) || audioUrl.Length > 2048)
        {
            throw new ArgumentException("Audio URL must be between 1 and 2048 characters.", nameof(audioUrl));
        }

        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Audio duration must be positive.");
        }
        if (!Enum.IsDefined(source) ||
            ((source == AudioSource.Tts) != (translationId is not null)))
        {
            throw new ArgumentException("Audio source and translation reference do not match.", nameof(source));
        }

        AudioUrl = audioUrl.Trim();
        DurationSeconds = durationSeconds;
        Source = source;
        TranslationId = translationId;
    }

    public Guid Id { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid LanguageId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Transcript { get; private set; } = string.Empty;
    public string AudioUrl { get; private set; } = string.Empty;
    public int DurationSeconds { get; private set; }
    public bool IsActive { get; private set; }
    public AudioSource Source { get; private set; }
    public Guid? TranslationId { get; private set; }
    public Location Location { get; private set; } = null!;
    public Language Language { get; private set; } = null!;
    public LocationTranslation? Translation { get; private set; }
}
