using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using QOL.Application.Commerce;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Services;

public sealed class OrderFulfillmentService(
    QolDbContext db,
    IEmailProvider emailProvider,
    ILogger<OrderFulfillmentService> logger) : IOrderFulfillmentService
{
    public async Task<OrderFulfillmentResult> VerifyAsync(
        Guid orderId,
        Guid? reviewerUserId,
        string? note,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        if (!string.Equals(
                db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await db.Database.SqlQuery<Guid>(
                    $"SELECT [Id] AS [Value] FROM [SubscriptionOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId}")
                .SingleOrDefaultAsync(cancellationToken);
        }

        var wasPending = false;
        var subscriptionCreated = false;
        string? recipientEmail = null;
        string? planName = null;
        DateTimeOffset? subscriptionEndsAt = null;

        try
        {
            var order = await db.SubscriptionOrders
                .Include(item => item.Plan)
                .Include(item => item.User)
                .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
            if (order is null)
            {
                return OrderFulfillmentResult.NotFound;
            }

            if (order.Status == SubscriptionOrderStatus.Rejected)
            {
                return OrderFulfillmentResult.Rejected;
            }

            wasPending = order.Status == SubscriptionOrderStatus.Pending;
            if (wasPending)
            {
                order.Review(SubscriptionOrderStatus.Verified, reviewerUserId, note);
            }

            var existingSubscription = await db.UserSubscriptions
                .SingleOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);
            if (existingSubscription is null)
            {
                var subscription = new UserSubscription(
                    order.UserId,
                    order.PlanId,
                    order.Id,
                    order.PlanName,
                    order.Currency,
                    order.Amount,
                    order.Plan.DurationDays);
                db.UserSubscriptions.Add(subscription);
                subscriptionCreated = true;
                recipientEmail = order.User.Email;
                planName = order.PlanName;
                subscriptionEndsAt = subscription.EndsAt;
            }

            if (!wasPending && !subscriptionCreated)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return OrderFulfillmentResult.AlreadyFulfilled;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            db.ChangeTracker.Clear();
            var alreadyFulfilled = await db.SubscriptionOrders
                .AsNoTracking()
                .AnyAsync(item => item.Id == orderId &&
                    item.Status == SubscriptionOrderStatus.Verified,
                    cancellationToken) &&
                await db.UserSubscriptions
                    .AsNoTracking()
                    .AnyAsync(item => item.OrderId == orderId, cancellationToken);
            if (!alreadyFulfilled)
            {
                throw;
            }

            return OrderFulfillmentResult.AlreadyFulfilled;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        if (recipientEmail is not null && planName is not null && subscriptionEndsAt is not null)
        {
            try
            {
                await emailProvider.SendOrderVerifiedAsync(
                    recipientEmail,
                    planName,
                    subscriptionEndsAt.Value,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not send order-verification notification for order {OrderId}.", orderId);
            }
        }

        return OrderFulfillmentResult.Fulfilled;
    }
}
