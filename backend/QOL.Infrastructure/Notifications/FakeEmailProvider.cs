using Microsoft.Extensions.Logging;
using QOL.Application.Commerce;

namespace QOL.Infrastructure.Notifications;

public sealed class FakeEmailProvider(ILogger<FakeEmailProvider> logger) : IEmailProvider
{
    public Task SendOrderVerifiedAsync(
        string recipientEmail,
        string planName,
        DateTimeOffset subscriptionEndsAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Fake email provider accepted an order-verification notification.");
        return Task.CompletedTask;
    }
}
