namespace QOL.Application.Audio;

public interface IActionLogService
{
    Task RecordAsync(
        Guid actorUserId,
        string action,
        string entityType,
        string? entityId,
        string? detail,
        CancellationToken cancellationToken);
}
