using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class AudioEndpoints
{
    private const long MaximumAudioSize = 20 * 1024 * 1024;
    private const int MaximumTranscriptLength = 50000;

    public static void MapAudioEndpoints(this WebApplication app, string audioDirectory)
    {
        var publicAudio = app.MapGroup("/api/locations/{locationId:guid}/audio")
            .WithTags("Audio");

        publicAudio.MapGet("/", async (
            Guid locationId,
            string? languageCode,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
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

                var audio = new AudioFile(
                    locationId,
                    request.LanguageId,
                    request.Title,
                    request.Transcript,
                    $"/media/audio/{fileName}",
                    request.DurationSeconds);
                db.AudioFiles.Add(audio);
                await db.SaveChangesAsync(cancellationToken);

                return TypedResults.Created(
                    $"/api/locations/{locationId}/audio",
                    await ToResponseAsync(db, audio.Id, cancellationToken));
            }
            catch
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                throw;
            }
        })
        .WithName("UploadLocationAudio")
        .WithSummary("Add translated audio content to a destination")
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

    [Range(1, 86400)]
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
