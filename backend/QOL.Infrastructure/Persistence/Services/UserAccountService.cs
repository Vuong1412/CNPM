using System.Data;
using Microsoft.EntityFrameworkCore;
using QOL.Application.Accounts;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Services;

public sealed class UserAccountService(QolDbContext dbContext) : IUserAccountService
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(
            user => user.Email == email.Trim().ToLowerInvariant(),
            cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<User?> RegisterAsync(
        string email,
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return null;
        }

        var user = new User(normalizedEmail, displayName);
        user.SetPasswordHash(passwordHash);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return user;
    }

    public async Task<FirstAdminCreationResult> CreateFirstAdminAsync(
        string email,
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await dbContext.Users.AnyAsync(user => user.Role == "Admin", cancellationToken))
        {
            return new FirstAdminCreationResult(FirstAdminCreationStatus.AlreadyExists, null);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return new FirstAdminCreationResult(FirstAdminCreationStatus.EmailAlreadyExists, null);
        }

        var user = new User(normalizedEmail, displayName);
        user.SetPasswordHash(passwordHash);
        user.AssignAdministratorRole();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new FirstAdminCreationResult(FirstAdminCreationStatus.Created, user);
    }

    public async Task UpdatePasswordHashAsync(
        User user,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        user.SetPasswordHash(passwordHash);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> UpdateProfileAsync(
        Guid userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.UpdateProfile(displayName);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }
}
