using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using QOL.Application.Locations;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.Endpoints;

public static class LocationEndpoints
{
    private const long MaximumImageSize = 5 * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegSignature = [255, 216, 255];

    public static void MapLocationEndpoints(this WebApplication app, string locationImageDirectory)
    {
        var locations = app.MapGroup("/api/locations").WithTags("Locations");

        locations.MapGet("/", async (
            string? search,
            ILocationService locationService,
            CancellationToken cancellationToken) =>
        {
            var items = await locationService.GetAllAsync(
                search,
                includeInactive: false,
                cancellationToken);
            return TypedResults.Ok(items.Select(ToResponse).ToArray());
        })
        .WithName("GetLocations")
        .WithSummary("Search active destinations")
        .WithDescription("Returns active destinations and optionally filters by name or address.")
        .Produces<LocationResponse[]>();

        locations.MapGet("/{id:guid}", async Task<Results<Ok<LocationResponse>, NotFound>> (
            Guid id,
            ILocationService locationService,
            CancellationToken cancellationToken) =>
        {
            var location = await locationService.GetActiveByIdAsync(id, cancellationToken);
            return location is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ToResponse(location));
        })
        .WithName("GetLocationById")
        .WithSummary("Get an active destination")
        .Produces<LocationResponse>()
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/media/locations/{fileName}", (string fileName) =>
        {
            if (!IsSafeImageFileName(fileName))
            {
                return Results.NotFound();
            }

            var root = GetImageDirectoryRoot(locationImageDirectory);
            var filePath = Path.GetFullPath(Path.Combine(locationImageDirectory, fileName));
            if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
            {
                return Results.NotFound();
            }

            return Results.File(filePath, GetImageContentType(fileName));
        })
        .WithName("GetLocationImage")
        .WithSummary("Get a public destination image")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        var adminLocations = app.MapGroup("/api/admin/locations")
            .WithTags("Administration", "Locations")
            .RequireRateLimiting("sensitive-write")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        adminLocations.MapGet("/", async (
            string? search,
            ILocationService locationService,
            CancellationToken cancellationToken) =>
        {
            var items = await locationService.GetAllAsync(
                search,
                includeInactive: true,
                cancellationToken);
            return TypedResults.Ok(items.Select(ToResponse).ToArray());
        })
        .WithName("GetAdminLocations")
        .WithSummary("List all destinations, including inactive ones")
        .Produces<LocationResponse[]>();

        adminLocations.MapPost("/", async Task<Created<LocationResponse>> (
            CreateLocationRequest request,
            ILocationService locationService,
            CancellationToken cancellationToken) =>
        {
            var location = await locationService.CreateAsync(
                ToInput(request),
                cancellationToken);
            return TypedResults.Created($"/api/locations/{location.Id}", ToResponse(location));
        })
        .WithName("CreateLocation")
        .WithSummary("Create a destination")
        .Produces<LocationResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        adminLocations.MapPut("/{id:guid}", async Task<Results<Ok<LocationResponse>, NotFound>> (
            Guid id,
            UpdateLocationRequest request,
            ILocationService locationService,
            CancellationToken cancellationToken) =>
        {
            var location = await locationService.UpdateAsync(
                id,
                ToInput(request),
                cancellationToken);
            return location is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ToResponse(location));
        })
        .WithName("UpdateLocation")
        .WithSummary("Update a destination")
        .Produces<LocationResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        adminLocations.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id,
            ILocationService locationService,
            CancellationToken cancellationToken) =>
        {
            var deactivated = await locationService.DeactivateAsync(id, cancellationToken);
            return deactivated
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        })
        .WithName("DeactivateLocation")
        .WithSummary("Soft-delete a destination")
        .WithDescription("Marks the destination inactive so listening history and favorites remain intact.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        adminLocations.MapPost("/{locationId:guid}/images", async Task<IResult> (
            Guid locationId,
            [FromForm] UploadLocationImageRequest request,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Locations.AnyAsync(location => location.Id == locationId, cancellationToken))
            {
                return TypedResults.NotFound();
            }

            if (request.File.Length is <= 0 or > MaximumImageSize)
            {
                return ImageValidationError("Image files must be no larger than 5 MB.");
            }

            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!IsSupportedImageExtension(extension))
            {
                return ImageValidationError("Only PNG, JPG, and WEBP images are supported.");
            }

            await using (var input = request.File.OpenReadStream())
            {
                var header = new byte[12];
                var bytesRead = await input.ReadAsync(header, cancellationToken);
                if (!HasExpectedImageSignature(extension, header, bytesRead))
                {
                    return ImageValidationError("The image file signature does not match its extension.");
                }
            }

            Directory.CreateDirectory(locationImageDirectory);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.GetFullPath(Path.Combine(locationImageDirectory, fileName));
            var root = GetImageDirectoryRoot(locationImageDirectory);
            if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Image storage path is invalid");
            }

            try
            {
                await using (var output = new FileStream(
                                 filePath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 bufferSize: 81920,
                                 useAsync: true))
                {
                    await request.File.CopyToAsync(output, cancellationToken);
                }

                var nextDisplayOrder = (await db.LocationImages
                    .Where(image => image.LocationId == locationId)
                    .MaxAsync(image => (int?)image.DisplayOrder, cancellationToken) ?? -1) + 1;
                var locationImage = new LocationImage(
                    locationId,
                    $"/media/locations/{fileName}",
                    request.AltText,
                    nextDisplayOrder);
                db.LocationImages.Add(locationImage);
                await db.SaveChangesAsync(cancellationToken);

                return TypedResults.Created(
                    $"/api/locations/{locationId}",
                    ToResponse(locationImage));
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
        .WithName("UploadLocationImage")
        .WithSummary("Upload a location image")
        .RequireRateLimiting("upload")
        .Accepts<UploadLocationImageRequest>("multipart/form-data")
        .Produces<LocationImageResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound);

        adminLocations.MapDelete("/{locationId:guid}/images/{imageId:guid}", async Task<IResult> (
            Guid locationId,
            Guid imageId,
            QolDbContext db,
            CancellationToken cancellationToken) =>
        {
            var image = await db.LocationImages.SingleOrDefaultAsync(
                item => item.Id == imageId && item.LocationId == locationId,
                cancellationToken);
            if (image is null)
            {
                return TypedResults.NotFound();
            }

            var filePath = GetImageFilePath(image.ImageUrl, locationImageDirectory);
            if (filePath is null)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Stored image path is invalid");
            }

            db.LocationImages.Remove(image);
            await db.SaveChangesAsync(cancellationToken);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return TypedResults.NoContent();
        })
        .WithName("DeleteLocationImage")
        .WithSummary("Delete a location image and its file")
        .RequireRateLimiting("sensitive-write")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static LocationResponse ToResponse(Location location) =>
        new(
            location.Id,
            location.Name,
            location.Address,
            location.Latitude,
            location.Longitude,
            location.RadiusMeters,
            location.Priority,
            location.OriginalDescription,
            location.DirectionLink,
            location.Status.ToString().ToUpperInvariant(),
            location.CreatedAt,
            location.UpdatedAt,
            location.Images
                .OrderBy(image => image.DisplayOrder)
                .Select(ToResponse)
                .ToArray());

    private static LocationImageResponse ToResponse(LocationImage image) =>
        new(image.Id, image.ImageUrl, image.AltText, image.DisplayOrder, image.CreatedAt);

    private static LocationDetailsInput ToInput(CreateLocationRequest request) =>
        new(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.RadiusMeters,
            request.Priority,
            request.OriginalDescription,
            request.DirectionLink,
            Enum.Parse<LocationStatus>(request.Status, ignoreCase: true));

    private static LocationDetailsInput ToInput(UpdateLocationRequest request) =>
        new(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.RadiusMeters,
            request.Priority,
            request.OriginalDescription,
            request.DirectionLink,
            Enum.Parse<LocationStatus>(request.Status, ignoreCase: true));

    private static IResult ImageValidationError(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(UploadLocationImageRequest.File)] = [message]
        });

    private static bool IsSupportedImageExtension(string extension) =>
        extension is ".png" or ".jpg" or ".jpeg" or ".webp";

    private static bool HasExpectedImageSignature(string extension, byte[] header, int bytesRead) =>
        extension switch
        {
            ".png" => bytesRead >= PngSignature.Length &&
                header.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature),
            ".jpg" or ".jpeg" => bytesRead >= JpegSignature.Length &&
                header.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature),
            ".webp" => bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

    private static string GetImageContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

    private static bool IsSafeImageFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) &&
        IsSupportedImageExtension(Path.GetExtension(fileName).ToLowerInvariant());

    private static string GetImageDirectoryRoot(string imageDirectory)
    {
        var fullPath = Path.GetFullPath(imageDirectory);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static string? GetImageFilePath(string imageUrl, string imageDirectory)
    {
        const string prefix = "/media/locations/";
        if (!imageUrl.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fileName = imageUrl[prefix.Length..];
        if (!IsSafeImageFileName(fileName))
        {
            return null;
        }

        var root = GetImageDirectoryRoot(imageDirectory);
        var filePath = Path.GetFullPath(Path.Combine(imageDirectory, fileName));
        return filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? filePath : null;
    }
}

/// <summary>Payload for creating a destination.</summary>
public sealed record CreateLocationRequest : IValidatableObject
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, StringLength(500, MinimumLength = 1)]
    public required string Address { get; init; }

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; init; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; init; }

    [Range(1, 100000)]
    public int RadiusMeters { get; init; } = 100;

    public int Priority { get; init; }

    [StringLength(4000)]
    public string? OriginalDescription { get; init; }

    [StringLength(2048)]
    public string? DirectionLink { get; init; }

    [Required, RegularExpression("^(ACTIVE|INACTIVE|PENDING|REJECTED)$")]
    public string Status { get; init; } = "ACTIVE";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return LocationRequestValidation.Validate(Latitude, Longitude, DirectionLink);
    }
}

/// <summary>Payload for updating a destination.</summary>
public sealed record UpdateLocationRequest : IValidatableObject
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, StringLength(500, MinimumLength = 1)]
    public required string Address { get; init; }

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; init; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; init; }

    [Range(1, 100000)]
    public int RadiusMeters { get; init; } = 100;

    public int Priority { get; init; }

    [StringLength(4000)]
    public string? OriginalDescription { get; init; }

    [StringLength(2048)]
    public string? DirectionLink { get; init; }

    [Required, RegularExpression("^(ACTIVE|INACTIVE|PENDING|REJECTED)$")]
    public string Status { get; init; } = "ACTIVE";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return LocationRequestValidation.Validate(Latitude, Longitude, DirectionLink);
    }
}

internal static class LocationRequestValidation
{
    public static IEnumerable<ValidationResult> Validate(
        decimal? latitude,
        decimal? longitude,
        string? directionLink)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            yield return new ValidationResult(
                "Latitude and longitude must both be provided or omitted.",
                [nameof(CreateLocationRequest.Latitude), nameof(CreateLocationRequest.Longitude)]);
        }

        if (!string.IsNullOrWhiteSpace(directionLink) &&
            (!Uri.TryCreate(directionLink, UriKind.Absolute, out var directionUri) ||
             directionUri.Scheme is not ("http" or "https")))
        {
            yield return new ValidationResult(
                "Direction link must be an absolute HTTP or HTTPS URL.",
                [nameof(CreateLocationRequest.DirectionLink)]);
        }
    }
}

/// <summary>Destination data returned by the API.</summary>
public sealed record LocationResponse(
    Guid Id,
    string Name,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    int RadiusMeters,
    int Priority,
    string? OriginalDescription,
    string? DirectionLink,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    LocationImageResponse[] Images);

public sealed record UploadLocationImageRequest
{
    [Required]
    public required IFormFile File { get; init; }

    [StringLength(500)]
    public string? AltText { get; init; }
}

public sealed record LocationImageResponse(
    Guid Id,
    string ImageUrl,
    string? AltText,
    int DisplayOrder,
    DateTimeOffset CreatedAt);
