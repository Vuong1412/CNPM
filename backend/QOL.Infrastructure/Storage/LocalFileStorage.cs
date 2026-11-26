using System.Buffers.Binary;
using System.Text;
using QOL.Application.Audio;

namespace QOL.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private const long MaximumAudioSize = 20 * 1024 * 1024;
    private const int MaximumAudioDurationSeconds = 3600;
    private const string AudioMediaPrefix = "/media/audio/";
    private readonly string _audioDirectory;

    public LocalFileStorage(string audioDirectory)
    {
        _audioDirectory = Path.GetFullPath(audioDirectory);
        Directory.CreateDirectory(_audioDirectory);
    }

    public async Task<StoredAudioFile> SaveAudioAsync(
        Stream content,
        string extension,
        long length,
        int declaredDurationSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        extension = extension.ToLowerInvariant();
        if (length is <= 0 or > MaximumAudioSize)
        {
            throw new InvalidAudioFileException("Audio files must be larger than zero and no larger than 20 MB.");
        }

        if (!IsSupportedAudio(extension))
        {
            throw new InvalidAudioFileException("Upload an MP3, WAV, OGG, or M4A audio file.");
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var finalPath = Path.Combine(_audioDirectory, fileName);
        var temporaryPath = $"{finalPath}.uploading";
        var movedToFinalPath = false;
        try
        {
            long written = 0;
            var buffer = new byte[81920];
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: buffer.Length,
                useAsync: true))
            {
                int bytesRead;
                while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += bytesRead;
                    if (written > MaximumAudioSize)
                    {
                        throw new InvalidAudioFileException("Audio files must be no larger than 20 MB.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }
            }

            if (written != length)
            {
                throw new InvalidAudioFileException("The uploaded audio length does not match its content.");
            }

            var header = new byte[12];
            await using (var savedFile = File.OpenRead(temporaryPath))
            {
                var headerLength = await savedFile.ReadAsync(header, cancellationToken);
                if (!HasExpectedSignature(extension, header.AsSpan(0, headerLength)))
                {
                    throw new InvalidAudioFileException("The file contents do not match the selected audio format.");
                }
            }

            var durationSeconds = extension == ".wav"
                ? ReadWavDurationSeconds(temporaryPath)
                : declaredDurationSeconds;
            if (durationSeconds is null or <= 0 or > MaximumAudioDurationSeconds)
            {
                throw new InvalidAudioFileException(
                    extension == ".wav"
                        ? "The WAV file has no valid duration or exceeds one hour."
                        : "Audio duration must be between 1 second and 1 hour.");
            }

            File.Move(temporaryPath, finalPath);
            movedToFinalPath = true;
            return new StoredAudioFile(
                fileName,
                $"{AudioMediaPrefix}{fileName}",
                durationSeconds.Value);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            if (movedToFinalPath && File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            throw;
        }
    }

    public Task<Stream?> OpenAudioAsync(string audioUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveAudioPath(audioUrl);
        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAudioAsync(string audioUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveAudioPath(audioUrl);
        if (path is null)
        {
            throw new InvalidOperationException("Stored audio URL is invalid.");
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<AudioOrphanCleanupResult> CleanupOrphansAsync(
        IReadOnlyCollection<string> knownAudioUrls,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var knownFiles = knownAudioUrls
            .Select(ResolveAudioPath)
            .Where(path => path is not null)
            .Select(path => Path.GetFullPath(path!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphanPaths = Directory.Exists(_audioDirectory)
            ? Directory.EnumerateFiles(_audioDirectory)
                .Where(path => IsSupportedAudio(Path.GetExtension(path).ToLowerInvariant()))
                .Where(path => !knownFiles.Contains(Path.GetFullPath(path)))
                .ToArray()
            : Array.Empty<string>();

        foreach (var orphanPath in orphanPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!dryRun)
            {
                File.Delete(orphanPath);
            }
        }

        return Task.FromResult(new AudioOrphanCleanupResult(
            dryRun,
            orphanPaths.Select(Path.GetFileName).Where(name => name is not null).Cast<string>().ToArray()));
    }

    private string? ResolveAudioPath(string audioUrl)
    {
        if (!audioUrl.StartsWith(AudioMediaPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fileName = audioUrl[AudioMediaPrefix.Length..];
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            !IsSupportedAudio(Path.GetExtension(fileName).ToLowerInvariant()))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_audioDirectory, fileName));
        var root = _audioDirectory.EndsWith(Path.DirectorySeparatorChar) ||
            _audioDirectory.EndsWith(Path.AltDirectorySeparatorChar)
            ? _audioDirectory
            : _audioDirectory + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private static bool IsSupportedAudio(string extension) =>
        extension is ".mp3" or ".wav" or ".ogg" or ".m4a";

    private static bool HasExpectedSignature(string extension, ReadOnlySpan<byte> header) =>
        extension switch
        {
            ".mp3" => header.StartsWith("ID3"u8) ||
                (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0),
            ".wav" => header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header[8..12].SequenceEqual("WAVE"u8),
            ".ogg" => header.StartsWith("OggS"u8),
            ".m4a" => header.Length >= 8 && header[4..8].SequenceEqual("ftyp"u8),
            _ => false
        };

    private static int? ReadWavDurationSeconds(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual("RIFF"u8))
        {
            return null;
        }

        var riffSize = reader.ReadUInt32();
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual("WAVE"u8) ||
            riffSize < 4 ||
            riffSize + 8L > stream.Length)
        {
            return null;
        }

        var riffEnd = 8L + riffSize;
        uint? byteRate = null;
        uint? dataSize = null;
        while (stream.Position + 8 <= riffEnd)
        {
            var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var chunkSize = reader.ReadUInt32();
            var chunkStart = stream.Position;
            var paddedSize = chunkSize + (long)(chunkSize & 1);
            if (paddedSize > riffEnd - chunkStart)
            {
                return null;
            }

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                var format = reader.ReadBytes(16);
                byteRate = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(8, 4));
            }
            else if (chunkId == "data")
            {
                dataSize = chunkSize;
            }

            stream.Position = chunkStart + paddedSize;
            if (byteRate is not null && dataSize is not null)
            {
                break;
            }
        }

        if (byteRate is null or 0 || dataSize is null or 0)
        {
            return null;
        }

        var seconds = ((long)dataSize.Value + byteRate.Value - 1) / byteRate.Value;
        return seconds <= int.MaxValue ? (int)seconds : null;
    }
}
