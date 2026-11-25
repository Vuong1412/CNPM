using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/locations/{locationId:guid}/reviews", async (
            Guid locationId,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var reviews = await db.LocationReviews
                .AsNoTracking()
                .Where(review => review.LocationId == locationId &&
                    review.Location.Status == LocationStatus.Active &&
                    review.IsVisible)
                .OrderByDescending(review => review.CreatedAt)
                .Take(100)
                .Select(review => new ReviewResponse(
                    review.Id,
                    review.User.DisplayName,
                    review.Rating,
                    review.Comment,
                    review.CreatedAt))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(reviews);
        })
        .WithTags("Reviews")
        .WithName("GetLocationReviews")
        .WithSummary("List visible reviews for a destination")
        .Produces<ReviewResponse[]>();

        var userReviews = app.MapGroup("/api/me/locations/{locationId:guid}/review")
            .WithTags("Reviews")
            .RequireRateLimiting("sensitive-write")
            .RequireAuthorization();

        userReviews.MapGet("/", async Task<IResult> (
            Guid locationId,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var review = await db.LocationReviews
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.LocationId == locationId)
                .Select(item => new ReviewResponse(
                    item.Id,
                    displayName,
                    item.Rating,
                    item.Comment,
                    item.CreatedAt))
                .SingleOrDefaultAsync(cancellationToken);
            return review is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(review);
        })
        .WithName("GetMyLocationReview")
        .Produces<ReviewResponse>()
        .Produces(StatusCodes.Status404NotFound);

        userReviews.MapPut("/", async Task<IResult> (
            Guid locationId,
            UpsertReviewRequest request,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var locationExists = await db.Locations.AnyAsync(
                location => location.Id == locationId && location.Status == LocationStatus.Active,
                cancellationToken);
            if (!locationExists)
            {
                return TypedResults.NotFound();
            }

            var review = await db.LocationReviews.SingleOrDefaultAsync(
                item => item.UserId == userId && item.LocationId == locationId,
                cancellationToken);
            if (review is null)
            {
                review = new LocationReview(userId, locationId, request.Rating, request.Comment);
                db.LocationReviews.Add(review);
            }
            else
            {
                review.UpdateContent(request.Rating, request.Comment);
            }

            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(new ReviewResponse(
                review.Id,
                principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                review.Rating,
                review.Comment,
                review.CreatedAt));
        })
        .WithName("UpsertMyLocationReview")
        .WithSummary("Create or update the signed-in user's review")
        .Produces<ReviewResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        userReviews.MapDelete("/", async Task<IResult> (
            Guid locationId,
            ClaimsPrincipal principal,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var review = await db.LocationReviews.SingleOrDefaultAsync(
                item => item.UserId == userId && item.LocationId == locationId,
                cancellationToken);
            if (review is not null)
            {
                db.LocationReviews.Remove(review);
                await db.SaveChangesAsync(cancellationToken);
            }

            return TypedResults.NoContent();
        })
        .WithName("DeleteMyLocationReview")
        .Produces(StatusCodes.Status204NoContent);

        var adminReviews = app.MapGroup("/api/admin/reviews")
            .WithTags("Administration", "Reviews")
            .RequireRateLimiting("sensitive-write")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminReviews.MapGet("/", async (
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var reviews = await db.LocationReviews
                .AsNoTracking()
                .OrderByDescending(review => review.CreatedAt)
                .Take(200)
                .Select(review => new AdminReviewResponse(
                    review.Id,
                    review.LocationId,
                    review.Location.Name,
                    review.User.Email,
                    review.Rating,
                    review.Comment,
                    review.CreatedAt,
                    review.IsVisible))
                .ToArrayAsync(cancellationToken);
            return TypedResults.Ok(reviews);
        })
        .WithName("GetAdminReviews")
        .Produces<AdminReviewResponse[]>();

        adminReviews.MapPatch("/{reviewId:guid}/visibility", async Task<IResult> (
            Guid reviewId,
            SetReviewVisibilityRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var review = await db.LocationReviews.SingleOrDefaultAsync(
                item => item.Id == reviewId,
                cancellationToken);
            if (review is null)
            {
                return TypedResults.NotFound();
            }

            review.SetVisibility(request.IsVisible);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("SetReviewVisibility")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public sealed record UpsertReviewRequest
{
    [Range(1, 5)]
    public required int Rating { get; init; }

    [StringLength(2000)]
    public string? Comment { get; init; }
}

public sealed record SetReviewVisibilityRequest
{
    public required bool IsVisible { get; init; }
}

public sealed record ReviewResponse(
    Guid Id,
    string DisplayName,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record AdminReviewResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    string UserEmail,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt,
    bool IsVisible);
