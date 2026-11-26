using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QOL.Application.Audio;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class TranslationEndpoints
{
    public static void MapTranslationEndpoints(this WebApplication app)
    {
        var adminTranslations = app.MapGroup("/api/admin/locations/{locationId:guid}/translations")
            .WithTags("Administration", "Translations")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("sensitive-write");

        adminTranslations.MapGet("/", async (
            Guid locationId,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var translations = await db.LocationTranslations
                .AsNoTracking()
                .Where(item => item.LocationId == locationId)
                .OrderBy(item => item.Language.Name)
                .Select(item => new LocationTranslationResponse(
                    item.Id,
                    item.LocationId,
                    item.LanguageId,
                    item.Language.Code,
                    item.Language.Name,
                    item.Text,
                    item.UpdatedAt))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(translations);
        })
        .WithName("GetAdminLocationTranslations")
        .Produces<LocationTranslationResponse[]>();

        adminTranslations.MapPut("/{languageId:guid}", async Task<IResult> (
            Guid locationId,
            Guid languageId,
            SaveLocationTranslationRequest request,
            ClaimsPrincipal principal,
            QolDbContext db,
            IFileStorage fileStorage,
            IActionLogService actionLogs,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAdminId(principal, out var adminUserId))
            {
                return TypedResults.Unauthorized();
            }

            if (!await db.Locations.AnyAsync(item => item.Id == locationId, cancellationToken))
            {
                return TypedResults.NotFound();
            }

            var language = await db.Languages.SingleOrDefaultAsync(
                item => item.Id == languageId && item.IsEnabled,
                cancellationToken);
            if (language is null)
            {
                return TypedResults.NotFound();
            }

            var translation = await db.LocationTranslations.SingleOrDefaultAsync(
                item => item.LocationId == locationId && item.LanguageId == languageId,
                cancellationToken);
            var changed = translation is null ||
                !string.Equals(translation.Text, request.Text.Trim(), StringComparison.Ordinal);
            if (translation is null)
            {
                translation = new LocationTranslation(locationId, languageId, request.Text);
                db.LocationTranslations.Add(translation);
            }
            else if (changed)
            {
                translation.UpdateText(request.Text);
            }

            var obsoleteAudio = changed
                ? await FindInvalidatedTtsAudioAsync(db, translation.Id, cancellationToken)
                : [];
            foreach (var item in obsoleteAudio.Where(item => item.HasHistory))
            {
                item.Audio.SetActive(false);
            }
            foreach (var item in obsoleteAudio.Where(item => !item.HasHistory))
            {
                db.AudioFiles.Remove(item.Audio);
            }

            await db.SaveChangesAsync(cancellationToken);
            foreach (var item in obsoleteAudio.Where(item => !item.HasHistory))
            {
                await fileStorage.DeleteAudioAsync(item.Audio.AudioUrl, cancellationToken);
            }

            foreach (var item in obsoleteAudio)
            {
                await actionLogs.RecordAsync(
                    adminUserId,
                    item.HasHistory ? "audio.translation-hidden" : "audio.translation-delete",
                    "AudioFile",
                    item.Audio.Id.ToString("D"),
                    "Translation text changed; previous TTS audio was invalidated.",
                    cancellationToken);
            }

            return TypedResults.Ok(ToResponse(translation, language));
        })
        .WithName("SaveLocationTranslation")
        .WithSummary("Create or update a location translation and invalidate old TTS audio")
        .Produces<LocationTranslationResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        adminTranslations.MapPost("/{translationId:guid}/audio/tts", async Task<IResult> (
            Guid locationId,
            Guid translationId,
            ClaimsPrincipal principal,
            QolDbContext db,
            ITextToSpeechProvider textToSpeech,
            IFileStorage fileStorage,
            IActionLogService actionLogs,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAdminId(principal, out var adminUserId))
            {
                return TypedResults.Unauthorized();
            }

            var translation = await db.LocationTranslations
                .Include(item => item.Language)
                .SingleOrDefaultAsync(
                    item => item.Id == translationId && item.LocationId == locationId,
                    cancellationToken);
            if (translation is null)
            {
                return TypedResults.NotFound();
            }

            byte[] generatedAudio;
            try
            {
                generatedAudio = await textToSpeech.SynthesizeAsync(
                    translation.Text,
                    translation.Language.Code,
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Text-to-speech is unavailable",
                    detail: exception.Message);
            }
            catch (HttpRequestException exception)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Text-to-speech provider failed",
                    detail: exception.Message);
            }

            StoredAudioFile storedAudio;
            await using (var content = new MemoryStream(generatedAudio, writable: false))
            {
                try
                {
                    storedAudio = await fileStorage.SaveAudioAsync(
                        content,
                        ".wav",
                        generatedAudio.LongLength,
                        declaredDurationSeconds: 1,
                        cancellationToken);
                }
                catch (InvalidAudioFileException exception)
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Text-to-speech returned invalid audio",
                        detail: exception.Message);
                }
            }

            var obsoleteAudio = await FindInvalidatedTtsAudioAsync(
                db,
                translation.Id,
                cancellationToken);
            foreach (var item in obsoleteAudio.Where(item => item.HasHistory))
            {
                item.Audio.SetActive(false);
            }
            foreach (var item in obsoleteAudio.Where(item => !item.HasHistory))
            {
                db.AudioFiles.Remove(item.Audio);
            }

            var audio = new AudioFile(
                locationId,
                translation.LanguageId,
                translation.Language.Name,
                translation.Text,
                storedAudio.AudioUrl,
                storedAudio.DurationSeconds,
                AudioSource.Tts,
                translation.Id);
            db.AudioFiles.Add(audio);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await fileStorage.DeleteAudioAsync(storedAudio.AudioUrl, CancellationToken.None);
                throw;
            }

            foreach (var item in obsoleteAudio.Where(item => !item.HasHistory))
            {
                await fileStorage.DeleteAudioAsync(item.Audio.AudioUrl, cancellationToken);
            }

            await actionLogs.RecordAsync(
                adminUserId,
                "audio.tts-create",
                "AudioFile",
                audio.Id.ToString("D"),
                $"translationId={translation.Id:D}",
                cancellationToken);
            return TypedResults.Created(
                $"/api/locations/{locationId}/audio",
                ToAudioResponse(audio, translation.Language));
        })
        .WithName("CreateTranslationAudio")
        .WithSummary("Generate and securely store TTS audio for a location translation")
        .RequireRateLimiting("upload")
        .Produces<AudioResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<InvalidatedTtsAudio[]> FindInvalidatedTtsAudioAsync(
        QolDbContext db,
        Guid translationId,
        CancellationToken cancellationToken)
    {
        var audioFiles = await db.AudioFiles
            .Where(item => item.TranslationId == translationId && item.Source == AudioSource.Tts)
            .ToArrayAsync(cancellationToken);
        var invalidated = new List<InvalidatedTtsAudio>(audioFiles.Length);
        foreach (var audio in audioFiles)
        {
            var hasHistory = await db.ListeningHistory.AnyAsync(
                history => history.AudioId == audio.Id,
                cancellationToken);
            invalidated.Add(new InvalidatedTtsAudio(audio, hasHistory));
        }

        return invalidated.ToArray();
    }

    private static LocationTranslationResponse ToResponse(
        LocationTranslation translation,
        Language language) =>
        new(
            translation.Id,
            translation.LocationId,
            translation.LanguageId,
            language.Code,
            language.Name,
            translation.Text,
            translation.UpdatedAt);

    private static AudioResponse ToAudioResponse(AudioFile audio, Language language) =>
        new(
            audio.Id,
            audio.LocationId,
            audio.LanguageId,
            language.Code,
            language.Name,
            language.NativeName,
            audio.Title,
            audio.Transcript,
            audio.AudioUrl,
            audio.DurationSeconds,
            audio.IsActive,
            "TTS",
            audio.TranslationId);

    private static bool TryGetAdminId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private sealed record InvalidatedTtsAudio(AudioFile Audio, bool HasHistory);
}

public sealed record SaveLocationTranslationRequest
{
    [Required, StringLength(50000, MinimumLength = 1)]
    public required string Text { get; init; }
}

public sealed record LocationTranslationResponse(
    Guid Id,
    Guid LocationId,
    Guid LanguageId,
    string LanguageCode,
    string LanguageName,
    string Text,
    DateTimeOffset UpdatedAt);
