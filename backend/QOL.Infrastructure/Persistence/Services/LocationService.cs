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
        var query = dbContext.Locations.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(location => location.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(location =>
                location.Name.Contains(searchTerm) ||
                location.Address.Contains(searchTerm));
        }

        return await query
            .OrderBy(location => location.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Location?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Locations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                location => location.Id == id && location.IsActive,
                cancellationToken);

    public async Task<Location> CreateAsync(
        string name,
        string address,
        CancellationToken cancellationToken)
    {
        var location = new Location(name, address);
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
        return location;
    }

    public async Task<Location?> UpdateAsync(
        Guid id,
        string name,
        string address,
        CancellationToken cancellationToken)
    {
        var location = await dbContext.Locations.SingleOrDefaultAsync(
            item => item.Id == id && item.IsActive,
            cancellationToken);
        if (location is null)
        {
            return null;
        }

        location.UpdateDetails(name, address);
        await dbContext.SaveChangesAsync(cancellationToken);
        return location;
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var location = await dbContext.Locations.SingleOrDefaultAsync(
            item => item.Id == id && item.IsActive,
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
