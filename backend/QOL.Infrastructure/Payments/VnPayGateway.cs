using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using QOL.Application.Payments;

namespace QOL.Infrastructure.Payments;

public sealed class VnPayGateway(IOptions<VnPayOptions> options) : IPaymentGateway
{
    private readonly VnPayOptions _options = options.Value;

    public string Provider => "VNPAY";

    public Task<string> CreatePaymentUrlAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConfigured();
        if (!string.Equals(request.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("VNPay payments require VND currency.");
        }

        var amountMinorUnits = checked((long)decimal.Round(
            request.Amount * 100,
            0,
            MidpointRounding.AwayFromZero));
        if (amountMinorUnits <= 0)
        {
            throw new InvalidOperationException("VNPay requires a positive payment amount.");
        }

        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amountMinorUnits.ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = NormalizeIpAddress(request.ClientIpAddress),
            ["vnp_IpnUrl"] = request.IpnUrl,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"QOL subscription order {request.OrderId:N}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = request.ReturnUrl,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_TxnRef"] = request.ProviderTransactionId,
            ["vnp_Version"] = "2.1.0"
        };
        var query = BuildQuery(fields);
        var signature = ComputeSignature(query, _options.HashSecret);
        return Task.FromResult($"{_options.PaymentUrl}?{query}&vnp_SecureHash={signature}");
    }

    public PaymentGatewayNotification VerifyNotification(
        IReadOnlyDictionary<string, string?> fields)
    {
        if (!IsConfigured ||
            !fields.TryGetValue("vnp_SecureHash", out var suppliedSignature) ||
            string.IsNullOrWhiteSpace(suppliedSignature))
        {
            return InvalidNotification();
        }

        var signedFields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            if (key is "vnp_SecureHash" or "vnp_SecureHashType" || value is null)
            {
                continue;
            }

            signedFields[key] = value;
        }

        byte[] expectedBytes;
        byte[] suppliedBytes;
        try
        {
            expectedBytes = Convert.FromHexString(ComputeSignature(BuildQuery(signedFields), _options.HashSecret));
            suppliedBytes = Convert.FromHexString(suppliedSignature);
        }
        catch (FormatException)
        {
            return InvalidNotification();
        }

        var signatureValid = expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes) &&
            string.Equals(
                GetValue(fields, "vnp_TmnCode"),
                _options.TmnCode,
                StringComparison.Ordinal);
        return new PaymentGatewayNotification(
            signatureValid,
            GetValue(fields, "vnp_TxnRef"),
            long.TryParse(GetValue(fields, "vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
                ? amount
                : null,
            GetValue(fields, "vnp_CurrCode"),
            GetValue(fields, "vnp_ResponseCode"),
            GetValue(fields, "vnp_TransactionStatus"),
            GetValue(fields, "vnp_TransactionNo"));
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.TmnCode) &&
        !string.IsNullOrWhiteSpace(_options.HashSecret) &&
        Uri.TryCreate(_options.PaymentUrl, UriKind.Absolute, out _) &&
        Uri.TryCreate(_options.ReturnUrl, UriKind.Absolute, out _) &&
        Uri.TryCreate(_options.IpnUrl, UriKind.Absolute, out _);

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("VNPay is enabled but its endpoint and credentials are incomplete.");
        }
    }

    private static PaymentGatewayNotification InvalidNotification() =>
        new(false, null, null, null, null, null, null);

    private static string? GetValue(IReadOnlyDictionary<string, string?> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    private static string NormalizeIpAddress(string address) =>
        string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join("&", fields.Select(field =>
            $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(field.Value)}"));

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
