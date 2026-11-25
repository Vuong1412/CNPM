using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class UserActivityEndpoints
{
    public static void MapUserActivityEndpoints(this WebApplication app)
    {
        var favorites = app.MapGroup("/api/me/favorites")
            .WithTags("Favorites")
            .RequireRateLimiting("sensitive-write")
            .RequireAuthorization();

        favorites.MapGet("/", async Task<Results<Ok<FavoriteResponse[]>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var items = await db.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .Select(favorite => new FavoriteResponse(
                    favorite.LocationId,
                    favorite.Location.Name,
                    favorite.Location.Address,
                    favorite.CreatedAt))
                .ToArrayAsync(cancellationToken);

            return TypedResults.Ok(items);
        })
        .WithName("GetMyFavorites")
        .WithSummary("List the signed-in user's favorite destinations")
        .Produces<FavoriteResponse[]>();

        favorites.MapPut("/{locationId:guid}", async Task<IResult> (
            Guid locationId,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var location = await db.Locations
                .SingleOrDefaultAsync(
                    item => item.Id == locationId && item.Status == LocationStatus.Active,
                    cancellationToken);
            if (location is null)
            {
                return TypedResults.NotFound();
            }

            var exists = await db.Favorites.AnyAsync(
                favorite => favorite.UserId == userId && favorite.LocationId == locationId,
                cancellationToken);
            if (!exists)
            {
                db.Favorites.Add(new Favorite(userId, locationId));
                await db.SaveChangesAsync(cancellationToken);
            }

            return TypedResults.NoContent();
        })
        .WithName("AddMyFavorite")
        .WithSummary("Save a destination to favorites")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        favorites.MapDelete("/{locationId:guid}", async Task<IResult> (
            Guid locationId,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var favorite = await db.Favorites.SingleOrDefaultAsync(
                item => item.UserId == userId && item.LocationId == locationId,
                cancellationToken);
            if (favorite is not null)
            {
                db.Favorites.Remove(favorite);
                await db.SaveChangesAsync(cancellationToken);
            }

            return TypedResults.NoContent();
        })
        .WithName("RemoveMyFavorite")
        .WithSummary("Remove a destination from favorites")
        .Produces(StatusCodes.Status204NoContent);

        var history = app.MapGroup("/api/me/listening-history")
            .WithTags("Listening history")
            .RequireRateLimiting("sensitive-write")
            .RequireAuthorization();

        history.MapGet("/", async Task<Results<Ok<ListeningHistoryResponse[]>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var items = await db.ListeningHistory
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.ListenedAt)
                .Take(100)
                .Select(item => new ListeningHistoryResponse(
                    item.Id,
                    item.LocationId,
                    item.Location.Name,
                    item.Language.Code,
                    item.Language.Name,
                    item.AudioFile.Title,
                    item.AudioFile.AudioUrl,
                    item.AudioFile.DurationSeconds,
                    item.DurationSeconds,
                    item.IsCompleted,
                    item.ListenedAt))
                .ToArrayAsync(cancellationToken);

            return TypedResults.Ok(items);
        })
        .WithName("GetMyListeningHistory")
        .WithSummary("List the latest 100 listening sessions")
        .Produces<ListeningHistoryResponse[]>();

        history.MapPost("/", async Task<IResult> (
            StartListeningRequest request,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var audio = await db.AudioFiles
                .AsNoTracking()
                .Where(item => item.Id == request.AudioId &&
                    item.Location.Status == LocationStatus.Active &&
                    item.Language.IsEnabled)
                .Select(item => new { item.Id, item.LocationId, item.LanguageId })
                .SingleOrDefaultAsync(cancellationToken);
            if (audio is null)
            {
                return TypedResults.NotFound();
            }

            var historyItem = new ListeningHistory(
                userId,
                audio.LocationId,
                audio.Id,
                audio.LanguageId);
            db.ListeningHistory.Add(historyItem);
            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Created(
                $"/api/me/listening-history/{historyItem.Id}",
                new ListeningSessionResponse(historyItem.Id));
        })
        .WithName("StartListeningSession")
        .WithSummary("Record a new audio listening session")
        .Produces<ListeningSessionResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        history.MapPatch("/{historyId:guid}", async Task<IResult> (
            Guid historyId,
            UpdateListeningProgressRequest request,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var historyItem = await db.ListeningHistory
                .Include(item => item.AudioFile)
                .SingleOrDefaultAsync(
                    item => item.Id == historyId && item.UserId == userId,
                    cancellationToken);
            if (historyItem is null)
            {
                return TypedResults.NotFound();
            }

            if (request.DurationSeconds > historyItem.AudioFile.DurationSeconds)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.DurationSeconds)] =
                    [
                        "Listening progress cannot exceed the audio duration."
                    ]
                });
            }

            historyItem.UpdateProgress(request.DurationSeconds, request.IsCompleted);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("UpdateListeningProgress")
        .WithSummary("Update progress for an owned listening session")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public sealed record FavoriteResponse(
    Guid LocationId,
    string Name,
    string Address,
    DateTimeOffset CreatedAt);

public sealed record StartListeningRequest
{
    [Required]
    public required Guid AudioId { get; init; }
}

public sealed record UpdateListeningProgressRequest
{
    [Range(0, 86400)]
    public required int DurationSeconds { get; init; }

    public bool IsCompleted { get; init; }
}

public sealed record ListeningSessionResponse(Guid Id);

public sealed record ListeningHistoryResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    string LanguageCode,
    string LanguageName,
    string AudioTitle,
    string AudioUrl,
    int AudioDurationSeconds,
    int DurationSeconds,
    bool IsCompleted,
    DateTimeOffset ListenedAt);
