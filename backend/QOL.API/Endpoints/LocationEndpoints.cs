using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using QOL.Application.Locations;
using QOL.Domain.Entities;

namespace QOL.API.Endpoints;

public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this WebApplication app)
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

        var adminLocations = app.MapGroup("/api/admin/locations")
            .WithTags("Administration", "Locations")
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
                request.Name,
                request.Address,
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
                request.Name,
                request.Address,
                cancellationToken);
            return location is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ToResponse(location));
        })
        .WithName("UpdateLocation")
        .WithSummary("Update an active destination")
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
    }

    private static LocationResponse ToResponse(Location location) =>
        new(location.Id, location.Name, location.Address, location.CreatedAt, location.IsActive);
}

/// <summary>Payload for creating a destination.</summary>
public sealed record CreateLocationRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, StringLength(500, MinimumLength = 1)]
    public required string Address { get; init; }
}

/// <summary>Payload for updating a destination.</summary>
public sealed record UpdateLocationRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, StringLength(500, MinimumLength = 1)]
    public required string Address { get; init; }
}

/// <summary>Destination data returned by the API.</summary>
public sealed record LocationResponse(
    Guid Id,
    string Name,
    string Address,
    DateTimeOffset CreatedAt,
    bool IsActive);
