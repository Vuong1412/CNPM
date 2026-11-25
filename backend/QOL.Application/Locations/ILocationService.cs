using QOL.Domain.Entities;

namespace QOL.Application.Locations;

public interface ILocationService
{
    Task<IReadOnlyList<Location>> GetAllAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken);
    Task<Location?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Location> CreateAsync(string name, string address, CancellationToken cancellationToken);
    Task<Location?> UpdateAsync(
        Guid id,
        string name,
        string address,
        CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
