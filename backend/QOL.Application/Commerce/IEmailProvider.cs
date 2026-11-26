namespace QOL.Application.Commerce;

public interface IEmailProvider
{
    Task SendOrderVerifiedAsync(
        string recipientEmail,
        string planName,
        DateTimeOffset subscriptionEndsAt,
        CancellationToken cancellationToken);
}
