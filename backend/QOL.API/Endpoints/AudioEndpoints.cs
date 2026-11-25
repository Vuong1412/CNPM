using System.ComponentModel.DataAnnotations;
using System.Buffers.Binary;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class AudioEndpoints
{
    private const long MaximumAudioSize = 20 * 1024 * 1024;
    private const int MaximumAudioDurationSeconds = 3600;
    private const int MaximumTranscriptLength = 50000;

    public static void MapAudioEndpoints(this WebApplication app, string audioDirectory)
    {
        var publicAudio = app.MapGroup("/api/locations/{locationId:guid}/audio")
            .WithTags("Audio")
            .RequireAuthorization();

        publicAudio.MapGet("/", async Task<IResult> (
            Guid locationId,
            string? languageCode,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var hasActiveSubscription = await db.UserSubscriptions.AnyAsync(
                subscription => subscription.UserId == userId &&
                    subscription.IsActive &&
                    subscription.StartsAt <= now &&
                    subscription.EndsAt > now,
                cancellationToken);
            if (!hasActiveSubscription)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Active subscription required",
                    detail: "An active subscription is required to listen to audio guides.");
            }

            var query = db.AudioFiles
                .AsNoTracking()
                .Where(audio => audio.LocationId == locationId &&
                    audio.IsActive &&
                    audio.Location.IsActive &&
                    audio.Language.IsEnabled);

            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                var normalizedCode = languageCode.Trim().ToLowerInvariant();
                query = query.Where(audio => audio.Language.Code == normalizedCode);
            }

            var audioFiles = await query
                .OrderBy(audio => audio.Language.Name)
                .ThenBy(audio => audio.Title)
                .Select(audio => new AudioResponse(
                    audio.Id,
                    audio.LocationId,
                    audio.LanguageId,
                    audio.Language.Code,
                    audio.Language.Name,
                    audio.Language.NativeName,
                    audio.Title,
                    audio.Transcript,
                    audio.AudioUrl,
                    audio.DurationSeconds,
                    audio.IsActive))
                .ToArrayAsync(cancellationToken);

            return TypedResults.Ok(audioFiles);
        })
        .WithName("GetLocationAudio")
        .WithSummary("List published audio guides for a destination")
        .Produces<AudioResponse[]>();

        app.MapGet("/media/audio/{fileName}", async Task<IResult> (
            string fileName,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            {
                return TypedResults.NotFound();
            }

            var mediaUrl = $"/media/audio/{fileName}";
            var audio = await db.AudioFiles
                .AsNoTracking()
                .Where(item => item.AudioUrl == mediaUrl)
                .Select(item => new
                {
                    AudioIsActive = item.IsActive,
                    LocationIsActive = item.Location.IsActive
                })
                .SingleOrDefaultAsync(cancellationToken);
            var isAdmin = principal.IsInRole("Admin");
            if (audio is null || (!isAdmin && (!audio.AudioIsActive || !audio.LocationIsActive)))
            {
                return TypedResults.NotFound();
            }

            if (!isAdmin)
            {
                if (!Guid.TryParse(
                        principal.FindFirstValue(ClaimTypes.NameIdentifier),
                        out var userId))
                {
                    return TypedResults.NotFound();
                }

                var now = DateTimeOffset.UtcNow;
                var hasActiveSubscription = await db.UserSubscriptions.AnyAsync(
                    subscription => subscription.UserId == userId &&
                        subscription.IsActive &&
                        subscription.StartsAt <= now &&
                        subscription.EndsAt > now,
                    cancellationToken);
                if (!hasActiveSubscription)
                {
                    return TypedResults.NotFound();
                }
            }

            var root = Path.GetFullPath(audioDirectory) + Path.DirectorySeparatorChar;
            var filePath = Path.GetFullPath(Path.Combine(audioDirectory, fileName));
            if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
            {
                return TypedResults.NotFound();
            }

            var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                _ => "application/octet-stream"
            };
            var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            return TypedResults.File(stream, contentType, enableRangeProcessing: true);
        })
        .WithName("GetAudioMedia")
        .WithSummary("Stream audio after checking its publication status")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        var adminAudio = app.MapGroup("/api/admin/locations/{locationId:guid}/audio")
            .WithTags("Administration", "Audio")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminAudio.MapGet("/", async (
            Guid locationId,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var audioFiles = await db.AudioFiles
                .AsNoTracking()
                .Where(audio => audio.LocationId == locationId)
                .OrderBy(audio => audio.Language.Name)
                .ThenBy(audio => audio.Title)
                .Select(audio => new AudioResponse(
                    audio.Id,
                    audio.LocationId,
                    audio.LanguageId,
                    audio.Language.Code,
                    audio.Language.Name,
                    audio.Language.NativeName,
                    audio.Title,
                    audio.Transcript,
                    audio.AudioUrl,
                    audio.DurationSeconds,
                    audio.IsActive))
                .ToArrayAsync(cancellationToken);

            return TypedResults.Ok(audioFiles);
        })
        .WithName("GetAdminLocationAudio")
        .WithSummary("List all audio guides for a destination")
        .Produces<AudioResponse[]>();

        adminAudio.MapPost("/", async Task<IResult> (
            Guid locationId,
            [FromForm] UploadAudioRequest request,
            QolDbContext db,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            if (request.File.Length is <= 0 or > MaximumAudioSize)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.File)] = ["Audio files must be no larger than 20 MB."]
                });
            }

            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!IsSupportedAudio(extension))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.File)] = ["Upload an MP3, WAV, OGG, or M4A audio file."]
                });
            }

            if (!await db.Locations.AnyAsync(
                    location => location.Id == locationId && location.IsActive,
                    cancellationToken))
            {
                return TypedResults.NotFound();
            }

            if (!await db.Languages.AnyAsync(
                    language => language.Id == request.LanguageId && language.IsEnabled,
                    cancellationToken))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.LanguageId)] = ["Choose an enabled language."]
                });
            }

            await using (var signatureStream = request.File.OpenReadStream())
            {
                var header = new byte[12];
                var bytesRead = await signatureStream.ReadAsync(header, cancellationToken);
                if (!HasExpectedSignature(extension, header.AsSpan(0, bytesRead)))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.File)] = ["The file contents do not match the selected audio format."]
                    });
                }
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(audioDirectory, fileName);
            Directory.CreateDirectory(audioDirectory);
            var databaseSaved = false;
            try
            {
                await using (var destination = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await request.File.CopyToAsync(destination, cancellationToken);
                }

                var durationSeconds = extension == ".wav"
                    ? ReadWavDurationSeconds(filePath)
                    : request.DurationSeconds;
                if (durationSeconds is null or <= 0 or > MaximumAudioDurationSeconds)
                {
                    File.Delete(filePath);
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.File)] =
                        [
                            extension == ".wav"
                                ? "The WAV file has no valid duration or exceeds one hour."
                                : "Audio duration must be between 1 second and 1 hour."
                        ]
                    });
                }

                var audio = new AudioFile(
                    locationId,
                    request.LanguageId,
                    request.Title,
                    request.Transcript,
                    $"/media/audio/{fileName}",
                    durationSeconds.Value);
                db.AudioFiles.Add(audio);
                await db.SaveChangesAsync(cancellationToken);
                databaseSaved = true;

                return TypedResults.Created(
                    $"/api/locations/{locationId}/audio",
                    await ToResponseAsync(db, audio.Id, cancellationToken));
            }
            catch
            {
                if (!databaseSaved && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                throw;
            }
        })
        .WithName("UploadLocationAudio")
        .WithSummary("Add translated audio content to a destination")
        .RequireRateLimiting("upload")
        .Accepts<UploadAudioRequest>("multipart/form-data")
        .Produces<AudioResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound);

        adminAudio.MapPut("/{audioId:guid}", async Task<IResult> (
            Guid locationId,
            Guid audioId,
            UpdateAudioContentRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var audio = await db.AudioFiles.SingleOrDefaultAsync(
                item => item.Id == audioId && item.LocationId == locationId,
                cancellationToken);
            if (audio is null)
            {
                return TypedResults.NotFound();
            }

            audio.UpdateContent(request.Title, request.Transcript);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(await ToResponseAsync(db, audio.Id, cancellationToken));
        })
        .WithName("UpdateAudioContent")
        .WithSummary("Edit an audio guide title and translated transcript")
        .RequireRateLimiting("sensitive-write")
        .Produces<AudioResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        adminAudio.MapPatch("/{audioId:guid}/visibility", async Task<IResult> (
            Guid locationId,
            Guid audioId,
            UpdateAudioVisibilityRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var audio = await db.AudioFiles.SingleOrDefaultAsync(
                item => item.Id == audioId && item.LocationId == locationId,
                cancellationToken);
            if (audio is null)
            {
                return TypedResults.NotFound();
            }

            audio.SetActive(request.IsActive);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("SetAudioVisibility")
        .WithSummary("Publish or hide an audio guide")
        .RequireRateLimiting("sensitive-write")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();
    }

    private static async Task<AudioResponse> ToResponseAsync(
        QolDbContext db,
        Guid audioId,
        CancellationToken cancellationToken) =>
        await db.AudioFiles
            .AsNoTracking()
            .Where(audio => audio.Id == audioId)
            .Select(audio => new AudioResponse(
                audio.Id,
                audio.LocationId,
                audio.LanguageId,
                audio.Language.Code,
                audio.Language.Name,
                audio.Language.NativeName,
                audio.Title,
                audio.Transcript,
                audio.AudioUrl,
                audio.DurationSeconds,
                audio.IsActive))
            .SingleAsync(cancellationToken);

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

public sealed record UploadAudioRequest
{
    public const int MaximumTranscriptLength = 50000;

    [Required, StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required, StringLength(MaximumTranscriptLength, MinimumLength = 1)]
    public required string Transcript { get; init; }

    [Required]
    public required Guid LanguageId { get; init; }

    public required int DurationSeconds { get; init; }

    [Required]
    public required IFormFile File { get; init; }
}

public sealed record UpdateAudioContentRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required, StringLength(UploadAudioRequest.MaximumTranscriptLength, MinimumLength = 1)]
    public required string Transcript { get; init; }
}

public sealed record UpdateAudioVisibilityRequest
{
    public required bool IsActive { get; init; }
}

public sealed record AudioResponse(
    Guid Id,
    Guid LocationId,
    Guid LanguageId,
    string LanguageCode,
    string LanguageName,
    string LanguageNativeName,
    string Title,
    string Transcript,
    string AudioUrl,
    int DurationSeconds,
    bool IsActive);
