namespace QOL.Domain.Entities;

public sealed class ActionLog
{
    private ActionLog()
    {
    }

    public ActionLog(
        Guid actorUserId,
        string action,
        string entityType,
        string? entityId = null,
        string? detail = null)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actorUserId));
        }

        ActorUserId = actorUserId;
        Action = Validate(action, 64, nameof(action));
        EntityType = Validate(entityType, 64, nameof(entityType));
        EntityId = ValidateOptional(entityId, 100, nameof(entityId));
        Detail = ValidateOptional(detail, 1000, nameof(detail));
        Id = Guid.NewGuid();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    private static string Validate(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new ArgumentException($"Value must be between 1 and {maxLength} characters.", parameterName);
        }

        return value.Trim();
    }

    private static string? ValidateOptional(string? value, int maxLength, string parameterName)
    {
        if (value?.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public User Actor { get; private set; } = null!;
}
