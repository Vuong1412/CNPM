namespace QOL.Application.Audio;

public interface IFileStorage
{
    Task<StoredAudioFile> SaveAudioAsync(
        Stream content,
        string extension,
        long length,
        int declaredDurationSeconds,
        CancellationToken cancellationToken);

    Task<Stream?> OpenAudioAsync(string audioUrl, CancellationToken cancellationToken);

    Task DeleteAudioAsync(string audioUrl, CancellationToken cancellationToken);

    Task<AudioOrphanCleanupResult> CleanupOrphansAsync(
        IReadOnlyCollection<string> knownAudioUrls,
        bool dryRun,
        CancellationToken cancellationToken);
}

public sealed record StoredAudioFile(string FileName, string AudioUrl, int DurationSeconds);

public sealed record AudioOrphanCleanupResult(bool DryRun, string[] FileNames);

public sealed class InvalidAudioFileException(string message) : Exception(message);
