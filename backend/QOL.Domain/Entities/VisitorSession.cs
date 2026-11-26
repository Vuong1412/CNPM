namespace QOL.Domain.Entities;

public sealed class VisitorSession
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private VisitorSession()
    {
    }

    public VisitorSession(Guid deviceId, DateTimeOffset? now = null)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        Id = Guid.NewGuid();
        DeviceId = deviceId;
        StartedAt = timestamp;
        LastSeenAt = timestamp;
        ExpiresAt = timestamp.Add(SessionLifetime);
    }

    public void SetLanguage(Guid languageId, DateTimeOffset? now = null)
    {
        if (languageId == Guid.Empty)
        {
            throw new ArgumentException("Language ID cannot be empty.", nameof(languageId));
        }

        LanguageId = languageId;
        Touch(now);
    }

    public void Touch(DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        LastSeenAt = timestamp;
        ExpiresAt = timestamp.Add(SessionLifetime);
    }

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid? LanguageId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Language? Language { get; private set; }
    public ICollection<ListeningHistory> ListeningHistory { get; private set; } =
        new List<ListeningHistory>();
}
