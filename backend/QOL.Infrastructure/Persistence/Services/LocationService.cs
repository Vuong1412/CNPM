using Microsoft.EntityFrameworkCore;
using QOL.Application.Locations;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Services;

public sealed class LocationService(QolDbContext dbContext) : ILocationService
{
    public async Task<IReadOnlyList<Location>> GetAllAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Locations
            .AsNoTracking()
            .Include(location => location.Images)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(location => location.Status == LocationStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(location =>
                location.Name.Contains(searchTerm) ||
                location.Address.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(location => location.Priority)
            .ThenBy(location => location.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Location?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Locations
            .AsNoTracking()
            .Include(location => location.Images)
            .SingleOrDefaultAsync(
                location => location.Id == id && location.Status == LocationStatus.Active,
                cancellationToken);

    public async Task<Location> CreateAsync(
        LocationDetailsInput details,
        CancellationToken cancellationToken)
    {
        var location = new Location(
            details.Name,
            details.Address,
            details.Latitude,
            details.Longitude,
            details.RadiusMeters,
            details.Priority,
            details.OriginalDescription,
            details.DirectionLink,
            details.Status);
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
        return location;
    }

    public async Task<Location?> UpdateAsync(
        Guid id,
        LocationDetailsInput details,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.Locations
            .Include(item => item.Images)
            .SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (location is null)
        {
            return null;
        }

        location.UpdateDetails(
            details.Name,
            details.Address,
            details.Latitude,
            details.Longitude,
            details.RadiusMeters,
            details.Priority,
            details.OriginalDescription,
            details.DirectionLink,
            details.Status);
        await dbContext.SaveChangesAsync(cancellationToken);
        return location;
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var location = await dbContext.Locations.SingleOrDefaultAsync(
            item => item.Id == id && item.Status != LocationStatus.Inactive,
            cancellationToken);
        if (location is null)
        {
            return false;
        }

        location.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
