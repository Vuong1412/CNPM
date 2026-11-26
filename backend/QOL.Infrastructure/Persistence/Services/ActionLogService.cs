using QOL.Application.Audio;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Services;

public sealed class ActionLogService(QolDbContext db) : IActionLogService
{
    public async Task RecordAsync(
        Guid actorUserId,
        string action,
        string entityType,
        string? entityId,
        string? detail,
        CancellationToken cancellationToken)
    {
        db.ActionLogs.Add(new ActionLog(actorUserId, action, entityType, entityId, detail));
        await db.SaveChangesAsync(cancellationToken);
    }
}
