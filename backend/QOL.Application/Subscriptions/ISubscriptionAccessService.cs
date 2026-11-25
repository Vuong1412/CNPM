namespace QOL.Application.Subscriptions;

public sealed record AvailableSubscriptionPlan(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int DurationDays);

public sealed record SubscriptionAccessResult(
    bool HasAccess,
    IReadOnlyList<AvailableSubscriptionPlan> AvailablePlans);

public sealed record UserSubscriptionStatus(
    Guid Id,
    string PlanName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive);

public interface ISubscriptionAccessService
{
    Task<SubscriptionAccessResult> CheckAccessAsync(
        Guid? userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSubscriptionStatus>> GetUserSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
