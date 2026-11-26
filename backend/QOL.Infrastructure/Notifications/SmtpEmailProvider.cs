using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using QOL.Application.Commerce;

namespace QOL.Infrastructure.Notifications;

public sealed class SmtpEmailProvider(IOptions<EmailOptions> options) : IEmailProvider
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendOrderVerifiedAsync(
        string recipientEmail,
        string planName,
        DateTimeOffset subscriptionEndsAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException("SMTP host and sender address must be configured.");
        }

        using var message = new MailMessage(_options.From, recipientEmail)
        {
            Subject = "Your QOL subscription is active",
            Body = $"Your {planName} subscription is active until {subscriptionEndsAt:O}.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.UserName, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
