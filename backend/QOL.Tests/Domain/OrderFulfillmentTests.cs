using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QOL.Application.Commerce;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;
using QOL.Infrastructure.Persistence.Services;
using Xunit;

namespace QOL.Tests.Domain;

public sealed class OrderFulfillmentTests
{
    [Fact]
    public async Task EmailFailureDoesNotUndoOrderVerificationOrSubscription()
    {
        var options = new DbContextOptionsBuilder<QolDbContext>()
            .UseInMemoryDatabase($"order-fulfillment-{Guid.NewGuid():N}")
            .Options;
        await using var db = new QolDbContext(options);
        var user = new User("buyer@example.com", "Buyer");
        var plan = new SubscriptionPlan("Monthly", "One month", 100, "VND", 30);
        var order = new SubscriptionOrder(user.Id, plan);
        db.AddRange(user, plan, order);
        await db.SaveChangesAsync();

        var fulfillment = new OrderFulfillmentService(
            db,
            new FailingEmailProvider(),
            NullLogger<OrderFulfillmentService>.Instance);

        var result = await fulfillment.VerifyAsync(
            order.Id,
            reviewerUserId: null,
            note: "verified by payment provider",
            CancellationToken.None);

        Assert.Equal(OrderFulfillmentResult.Fulfilled, result);
        Assert.Equal(SubscriptionOrderStatus.Verified, order.Status);
        Assert.Single(await db.UserSubscriptions.ToListAsync());
    }

    private sealed class FailingEmailProvider : IEmailProvider
    {
        public Task SendOrderVerifiedAsync(
            string recipientEmail,
            string planName,
            DateTimeOffset subscriptionEndsAt,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated email provider outage.");
    }
}
