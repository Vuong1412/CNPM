using QOL.Domain.Entities;

namespace QOL.Application.Accounts;

public enum FirstAdminCreationStatus
{
    Created,
    AlreadyExists,
    EmailAlreadyExists
}

public sealed record FirstAdminCreationResult(
    FirstAdminCreationStatus Status,
    User? User);

public interface IUserAccountService
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> RegisterAsync(
        string email,
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken);
    Task<FirstAdminCreationResult> CreateFirstAdminAsync(
        string email,
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken);
    Task UpdatePasswordHashAsync(User user, string passwordHash, CancellationToken cancellationToken);
    Task<User?> UpdateProfileAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken);
}
