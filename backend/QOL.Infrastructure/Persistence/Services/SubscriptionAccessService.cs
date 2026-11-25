using Microsoft.EntityFrameworkCore;
using QOL.Application.Subscriptions;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Services;

public sealed class SubscriptionAccessService(QolDbContext dbContext) : ISubscriptionAccessService
{
    public async Task<SubscriptionAccessResult> CheckAccessAsync(
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (userId.HasValue)
        {
            var subscriptions = await LoadAndExpireSubscriptionsAsync(
                userId.Value,
                now,
                cancellationToken);

            if (subscriptions.Any(subscription =>
                    subscription.IsActive &&
                    subscription.StartsAt <= now &&
                    subscription.EndsAt > now))
            {
                return new SubscriptionAccessResult(true, []);
            }
        }

        var availablePlans = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.Price)
            .Select(plan => new AvailableSubscriptionPlan(
                plan.Id,
                plan.Name,
                plan.Description,
                plan.Price,
                plan.Currency,
                plan.DurationDays))
            .ToArrayAsync(cancellationToken);

        return new SubscriptionAccessResult(false, availablePlans);
    }

    public async Task<IReadOnlyList<UserSubscriptionStatus>> GetUserSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await LoadAndExpireSubscriptionsAsync(userId, now, cancellationToken);

        return await dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.UserId == userId)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => new UserSubscriptionStatus(
                subscription.Id,
                subscription.PlanName,
                subscription.StartsAt,
                subscription.EndsAt,
                subscription.IsActive &&
                    subscription.StartsAt <= now &&
                    subscription.EndsAt > now))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<List<UserSubscription>> LoadAndExpireSubscriptionsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.UserSubscriptions
            .Where(subscription => subscription.UserId == userId && subscription.IsActive)
            .ToListAsync(cancellationToken);

        var expiredAny = false;
        foreach (var subscription in subscriptions)
        {
            var wasActive = subscription.IsActive;
            subscription.ExpireIfNeeded(now);
            expiredAny |= wasActive && !subscription.IsActive;
        }

        if (expiredAny)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return subscriptions;
    }
}
