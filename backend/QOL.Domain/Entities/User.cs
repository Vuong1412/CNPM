namespace QOL.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    public User(string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
        {
            throw new ArgumentException("Email must be between 1 and 320 characters.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 150)
        {
            throw new ArgumentException("Display name must be between 1 and 150 characters.", nameof(displayName));
        }

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
        Role = "User";
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public ICollection<Favorite> Favorites { get; private set; } = new List<Favorite>();
    public ICollection<ListeningHistory> ListeningHistory { get; private set; } = new List<ListeningHistory>();

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > 512)
        {
            throw new ArgumentException("Password hash must be between 1 and 512 characters.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    public void AssignAdministratorRole() => Role = "Admin";

    public void SetActive(bool isActive) => IsActive = isActive;

    public void UpdateProfile(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 150)
        {
            throw new ArgumentException("Display name must be between 1 and 150 characters.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
    }
}
