using QOL.Domain.Entities;

namespace QOL.Application.Locations;

public sealed record LocationDetailsInput(
    string Name,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    int RadiusMeters,
    int Priority,
    string? OriginalDescription,
    string? DirectionLink,
    LocationStatus Status);
