using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QOL.Application.Audio;
using QOL.Application.Subscriptions;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class AudioEndpoints
{
    public static void MapAudioEndpoints(this WebApplication app)
    {
        var publicAudio = app.MapGroup("/api/locations/{locationId:guid}/audio")
            .WithTags("Audio");

        publicAudio.MapGet("/", async Task<IResult> (
            Guid locationId,
            string? languageCode,
            ClaimsPrincipal principal,
            QolDbContext db,
            ISubscriptionAccessService subscriptionAccess,
            CancellationToken cancellationToken) =>
        {
            var isAdmin = principal.IsInRole("Admin");
            var locationIsActive = await db.Locations
                .AsNoTracking()
                .AnyAsync(
                    location => location.Id == locationId && location.Status == LocationStatus.Active,
                    cancellationToken);
            if (!locationIsActive)
            {
                return TypedResults.NotFound();
            }

            if (!isAdmin)
            {
                var userId = Guid.TryParse(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    out var parsedUserId)
                    ? parsedUserId
                    : (Guid?)null;
                var access = await subscriptionAccess.CheckAccessAsync(userId, cancellationToken);
                if (!access.HasAccess)
                {
                    return SubscriptionRequired(access, userId is null);
                }
            }

            var query = db.AudioFiles
                .AsNoTracking()
                .Where(audio => audio.LocationId == locationId &&
                    audio.IsActive &&
                    audio.Location.Status == LocationStatus.Active &&
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
                    audio.IsActive,
                    audio.Source == AudioSource.Tts ? "TTS" : "UPLOAD",
                    audio.TranslationId))
                .ToArrayAsync(cancellationToken);

            return TypedResults.Ok(audioFiles);
        })
        .WithName("GetLocationAudio")
        .WithSummary("List published audio guides for a destination")
        .Produces<AudioResponse[]>()
        .ProducesProblem(StatusCodes.Status402PaymentRequired)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/media/audio/{fileName}", async Task<IResult> (
            string fileName,
            ClaimsPrincipal principal,
            QolDbContext db,
            IFileStorage fileStorage,
            ISubscriptionAccessService subscriptionAccess,
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
                    LocationIsActive = item.Location.Status == LocationStatus.Active
                })
                .SingleOrDefaultAsync(cancellationToken);
            var isAdmin = principal.IsInRole("Admin");
            if (audio is null || (!isAdmin && (!audio.AudioIsActive || !audio.LocationIsActive)))
            {
                return TypedResults.NotFound();
            }

            if (!isAdmin)
            {
                var userId = Guid.TryParse(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    out var parsedUserId)
                    ? parsedUserId
                    : (Guid?)null;
                var access = await subscriptionAccess.CheckAccessAsync(userId, cancellationToken);
                if (!access.HasAccess)
                {
                    return SubscriptionRequired(access, userId is null);
                }
            }

            var stream = await fileStorage.OpenAudioAsync(mediaUrl, cancellationToken);
            if (stream is null)
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
            return TypedResults.File(stream, contentType, enableRangeProcessing: true);
        })
        .WithName("GetAudioMedia")
        .WithSummary("Stream audio after checking its publication status")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status402PaymentRequired)
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
                    audio.IsActive,
                    audio.Source == AudioSource.Tts ? "TTS" : "UPLOAD",
                    audio.TranslationId))
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
            IFileStorage fileStorage,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Locations.AnyAsync(
                    location => location.Id == locationId && location.Status == LocationStatus.Active,
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

            var (storedFile, error) = await SaveAudioFileAsync(
                request.File,
                request.DurationSeconds,
                fileStorage,
                cancellationToken);
            if (error is not null)
            {
                return error;
            }

            var savedFile = storedFile
                ?? throw new InvalidOperationException("Audio storage returned neither a file nor an error.");
            var databaseSaved = false;
            try
            {
                var audio = new AudioFile(
                    locationId,
                    request.LanguageId,
                    request.Title,
                    request.Transcript,
                    savedFile.AudioUrl,
                    savedFile.DurationSeconds);
                db.AudioFiles.Add(audio);
                await db.SaveChangesAsync(cancellationToken);
                databaseSaved = true;

                return TypedResults.Created(
                    $"/api/locations/{locationId}/audio",
                    await ToResponseAsync(db, audio.Id, cancellationToken));
            }
            catch
            {
                if (!databaseSaved)
                {
                    await fileStorage.DeleteAudioAsync(savedFile.AudioUrl, CancellationToken.None);
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

        adminAudio.MapPut("/{audioId:guid}/file", async Task<IResult> (
            Guid locationId,
            Guid audioId,
            [FromForm] ReplaceAudioFileRequest request,
            QolDbContext db,
            IFileStorage fileStorage,
            IActionLogService actionLogs,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
            {
                return TypedResults.Unauthorized();
            }

            var audio = await db.AudioFiles.SingleOrDefaultAsync(
                item => item.Id == audioId && item.LocationId == locationId,
                cancellationToken);
            if (audio is null)
            {
                return TypedResults.NotFound();
            }

            var oldAudioUrl = audio.AudioUrl;

            var (storedFile, error) = await SaveAudioFileAsync(
                request.File,
                request.DurationSeconds,
                fileStorage,
                cancellationToken);
            if (error is not null)
            {
                return error;
            }

            var savedFile = storedFile
                ?? throw new InvalidOperationException("Audio storage returned neither a file nor an error.");
            try
            {
                audio.ReplaceFile(
                    savedFile.AudioUrl,
                    savedFile.DurationSeconds,
                    AudioSource.Upload);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await fileStorage.DeleteAudioAsync(savedFile.AudioUrl, CancellationToken.None);
                throw;
            }

            await fileStorage.DeleteAudioAsync(oldAudioUrl, cancellationToken);
            await actionLogs.RecordAsync(
                actorUserId,
                "audio.replace",
                "AudioFile",
                audioId.ToString("D"),
                null,
                cancellationToken);

            return TypedResults.Ok(await ToResponseAsync(db, audio.Id, cancellationToken));
        })
        .WithName("ReplaceAudioFile")
        .WithSummary("Replace an audio file without changing its guide content")
        .RequireRateLimiting("upload")
        .Accepts<ReplaceAudioFileRequest>("multipart/form-data")
        .Produces<AudioResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

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

        adminAudio.MapDelete("/{audioId:guid}", async Task<IResult> (
            Guid locationId,
            Guid audioId,
            ClaimsPrincipal principal,
            QolDbContext db,
            IFileStorage fileStorage,
            IActionLogService actionLogs,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
            {
                return TypedResults.Unauthorized();
            }

            var audio = await db.AudioFiles.SingleOrDefaultAsync(
                item => item.Id == audioId && item.LocationId == locationId,
                cancellationToken);
            if (audio is null)
            {
                return TypedResults.NotFound();
            }

            var hasListeningHistory = await db.ListeningHistory.AnyAsync(
                history => history.AudioId == audioId,
                cancellationToken);
            if (hasListeningHistory)
            {
                audio.SetActive(false);
                await db.SaveChangesAsync(cancellationToken);
                await actionLogs.RecordAsync(
                    adminUserId,
                    "audio.delete-hidden",
                    "AudioFile",
                    audioId.ToString("D"),
                    "Audio has listening history; retained and hidden.",
                    cancellationToken);
                return TypedResults.NoContent();
            }

            var audioUrl = audio.AudioUrl;
            db.AudioFiles.Remove(audio);
            await db.SaveChangesAsync(cancellationToken);
            await fileStorage.DeleteAudioAsync(audioUrl, cancellationToken);
            await actionLogs.RecordAsync(
                adminUserId,
                "audio.delete",
                "AudioFile",
                audioId.ToString("D"),
                null,
                cancellationToken);

            return TypedResults.NoContent();
        })
        .WithName("DeleteAudio")
        .WithSummary("Delete audio without history, or hide it when history exists")
        .RequireRateLimiting("sensitive-write")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        var adminAudioCleanup = app.MapGroup("/api/admin/audio")
            .WithTags("Administration", "Audio")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminAudioCleanup.MapPost("/cleanup", async Task<IResult> (
            [FromQuery] bool dryRun,
            ClaimsPrincipal principal,
            QolDbContext db,
            IFileStorage fileStorage,
            IActionLogService actionLogs,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
            {
                return TypedResults.Unauthorized();
            }

            var knownUrls = await db.AudioFiles
                .AsNoTracking()
                .Select(audio => audio.AudioUrl)
                .ToArrayAsync(cancellationToken);
            var result = await fileStorage.CleanupOrphansAsync(
                knownUrls,
                dryRun,
                cancellationToken);
            await actionLogs.RecordAsync(
                actorUserId,
                "audio.orphan-cleanup",
                "AudioFile",
                null,
                $"dryRun={dryRun}; count={result.FileNames.Length}",
                cancellationToken);
            return TypedResults.Ok(new AudioOrphanCleanupResponse(
                result.DryRun,
                result.FileNames.Length,
                result.FileNames));
        })
        .WithName("CleanupOrphanAudioFiles")
        .WithSummary("List or delete audio files not referenced by audio records")
        .RequireRateLimiting("sensitive-write")
        .Produces<AudioOrphanCleanupResponse>()
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

    private static IResult SubscriptionRequired(
        SubscriptionAccessResult access,
        bool requiresAuthentication) =>
        Results.Problem(
            type: "subscription-required",
            title: "Subscription required",
            statusCode: StatusCodes.Status402PaymentRequired,
            detail: requiresAuthentication
                ? "Sign in and purchase an active subscription to listen to audio guides."
                : "An active subscription is required to listen to audio guides.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "subscription-required",
                ["requiresAuthentication"] = requiresAuthentication,
                ["availablePlans"] = access.AvailablePlans
            });

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
                audio.IsActive,
                audio.Source == AudioSource.Tts ? "TTS" : "UPLOAD",
                audio.TranslationId))
            .SingleAsync(cancellationToken);

    private static async Task<(StoredAudioFile? File, IResult? Error)> SaveAudioFileAsync(
        IFormFile file,
        int declaredDurationSeconds,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        try
        {
            var storedFile = await fileStorage.SaveAudioAsync(
                content,
                Path.GetExtension(file.FileName),
                file.Length,
                declaredDurationSeconds,
                cancellationToken);
            return (storedFile, null);
        }
        catch (InvalidAudioFileException exception)
        {
            return (null, TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(file)] = [exception.Message]
            }));
        }
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

public sealed record ReplaceAudioFileRequest
{
    [Required]
    public required int DurationSeconds { get; init; }

    [Required]
    public required IFormFile File { get; init; }
}

public sealed record AudioOrphanCleanupResponse(
    bool DryRun,
    int Count,
    string[] FileNames);

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
    bool IsActive,
    string Source,
    Guid? TranslationId);
