namespace QOL.Domain.Entities;

public sealed class Language
{
    private Language()
    {
    }

    public Language(string code, string name, string nativeName)
        : this(Guid.NewGuid(), code, name, nativeName)
    {
    }

    public Language(Guid id, string code, string name, string nativeName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Language ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 10)
        {
            throw new ArgumentException("Language code must be between 1 and 10 characters.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new ArgumentException("Language name must be between 1 and 100 characters.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(nativeName) || nativeName.Length > 100)
        {
            throw new ArgumentException("Native name must be between 1 and 100 characters.", nameof(nativeName));
        }

        Id = id;
        Code = code.Trim().ToLowerInvariant();
        Name = name.Trim();
        NativeName = nativeName.Trim();
        IsEnabled = true;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NativeName { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
}
