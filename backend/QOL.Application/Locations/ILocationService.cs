using QOL.Domain.Entities;

namespace QOL.Application.Locations;

public interface ILocationService
{
    Task<IReadOnlyList<Location>> GetAllAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken);
    Task<Location?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Location> CreateAsync(LocationDetailsInput details, CancellationToken cancellationToken);
    Task<Location?> UpdateAsync(
        Guid id,
        LocationDetailsInput details,
        CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
