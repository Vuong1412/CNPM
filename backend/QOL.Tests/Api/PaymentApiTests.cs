using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QOL.Application.Payments;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;
using QOL.Infrastructure.Payments;
using Xunit;

namespace QOL.Tests.Api;

public sealed class PaymentApiTests
{
    [Fact]
    public async Task IpNRejectsInvalidSignatureWithoutVerifyingOrder()
    {
        using var factory = new PaymentTestFactory();
        using var client = CreateCookieClient(factory);
        var fixture = await SeedOrderAsync(factory, client);
        var payment = await CreatePaymentAsync(factory, client, fixture.Order.Id);
        var fields = CreatePaidNotification(payment.ProviderTxnId, 150_000);
        fields["vnp_SecureHash"] = new string('0', 128);

        var response = await SendIpnAsync(client, fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("97", payload.RootElement.GetProperty("rspCode").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        Assert.Equal(SubscriptionOrderStatus.Pending, (await db.SubscriptionOrders.SingleAsync()).Status);
        Assert.Empty(await db.UserSubscriptions.ToArrayAsync());
    }

    [Fact]
    public async Task IpNRejectsAmountMismatchEvenWhenSignatureIsValid()
    {
        using var factory = new PaymentTestFactory();
        using var client = CreateCookieClient(factory);
        var fixture = await SeedOrderAsync(factory, client);
        var payment = await CreatePaymentAsync(factory, client, fixture.Order.Id);
        var fields = CreatePaidNotification(payment.ProviderTxnId, 149_900);
        SignNotification(fields);

        var response = await SendIpnAsync(client, fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("04", payload.RootElement.GetProperty("rspCode").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        Assert.Equal(SubscriptionOrderStatus.Pending, (await db.SubscriptionOrders.SingleAsync()).Status);
        Assert.Empty(await db.UserSubscriptions.ToArrayAsync());
        Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task SuccessfulRepeatedIpnCreatesOneSubscriptionAndEnablesAudio()
    {
        using var factory = new PaymentTestFactory();
        using var client = CreateCookieClient(factory);
        var fixture = await SeedOrderAsync(factory, client, includeAudio: true);
        var payment = await CreatePaymentAsync(factory, client, fixture.Order.Id);
        var fields = CreatePaidNotification(payment.ProviderTxnId, 150_000);
        SignNotification(fields);

        var firstResponse = await SendIpnAsync(client, fields);
        var replayResponse = await SendIpnAsync(client, fields);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        using var firstPayload = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        using var replayPayload = JsonDocument.Parse(await replayResponse.Content.ReadAsStringAsync());
        Assert.Equal("00", firstPayload.RootElement.GetProperty("rspCode").GetString());
        Assert.Equal("00", replayPayload.RootElement.GetProperty("rspCode").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            Assert.Equal(SubscriptionOrderStatus.Verified, (await db.SubscriptionOrders.SingleAsync()).Status);
            Assert.Single(await db.UserSubscriptions.ToArrayAsync());
            Assert.Equal(PaymentStatus.Paid, (await db.Payments.SingleAsync()).Status);
        }

        var audioList = await client.GetAsync($"/api/locations/{fixture.Location!.Id}/audio");
        Assert.Equal(HttpStatusCode.OK, audioList.StatusCode);
        var media = await client.GetAsync(fixture.AudioUrl!);
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
    }

    [Fact]
    public async Task PaymentLinkUsesServerOwnedOrderAndRejectsAnotherUsersOrder()
    {
        using var factory = new PaymentTestFactory();
        using var client = CreateCookieClient(factory);
        var fixture = await SeedOrderAsync(factory, client);
        var response = await PostWithAntiforgeryAsync(
            client,
            $"/api/me/orders/{fixture.Order.Id}/pay",
            new { amount = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<PaymentLinkResponse>();
        Assert.NotNull(link);
        Assert.Contains("vnp_Amount=150000", link.PaymentUrl, StringComparison.Ordinal);

        using var otherClient = CreateCookieClient(factory);
        var otherUser = new User("other@example.com", "Other");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            otherUser.SetPasswordHash(hasher.HashPassword(otherUser, "TestPassword!2026"));
            db.Users.Add(otherUser);
            await db.SaveChangesAsync();
        }
        await SignInAsync(otherClient, "other@example.com");
        var forbiddenOrder = await PostWithAntiforgeryAsync(
            otherClient,
            $"/api/me/orders/{fixture.Order.Id}/pay",
            new { });
        Assert.Equal(HttpStatusCode.NotFound, forbiddenOrder.StatusCode);
    }

    private static WebApplicationFactoryClientOptions CookieOptions() => new()
    {
        HandleCookies = true,
        BaseAddress = new Uri("http://localhost")
    };

    private static HttpClient CreateCookieClient(PaymentTestFactory factory) =>
        factory.CreateClient(CookieOptions());

    private static async Task<SeededOrder> SeedOrderAsync(
        PaymentTestFactory factory,
        HttpClient client,
        bool includeAudio = false)
    {
        var user = new User("buyer@example.com", "Buyer");
        var plan = new SubscriptionPlan("Monthly", "One month", 1500, "VND", 30);
        var order = new SubscriptionOrder(user.Id, plan);
        Location? location = null;
        string? audioUrl = null;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            user.SetPasswordHash(hasher.HashPassword(user, "TestPassword!2026"));
            db.Users.Add(user);
            db.SubscriptionPlans.Add(plan);
            db.SubscriptionOrders.Add(order);

            if (includeAudio)
            {
                location = new Location("Paid destination", "Test address");
                var language = new Language("vi", "Vietnamese", "Tiếng Việt");
                const string fileName = "paid-audio.wav";
                audioUrl = $"/media/audio/{fileName}";
                db.Locations.Add(location);
                db.Languages.Add(language);
                db.AudioFiles.Add(new AudioFile(
                    location.Id,
                    language.Id,
                    "Paid guide",
                    "Paid transcript",
                    audioUrl,
                    1));
                await File.WriteAllBytesAsync(
                    Path.Combine(factory.AudioDirectory, fileName),
                    CreatePcmWav());
            }

            await db.SaveChangesAsync();
        }

        await SignInAsync(client, "buyer@example.com");
        return new SeededOrder(order, location, audioUrl);
    }

    private static async Task<PaymentReference> CreatePaymentAsync(
        PaymentTestFactory factory,
        HttpClient client,
        Guid orderId)
    {
        var response = await PostWithAntiforgeryAsync(
            client,
            $"/api/me/orders/{orderId}/pay",
            new { amount = 1, currency = "XXX" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var paymentId = document.RootElement.GetProperty("paymentId").GetGuid();
        Assert.StartsWith(
            "https://sandbox.vnpayment.vn/",
            document.RootElement.GetProperty("paymentUrl").GetString(),
            StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var payment = await scope.ServiceProvider
            .GetRequiredService<QolDbContext>()
            .Payments
            .AsNoTracking()
            .SingleAsync(item => item.Id == paymentId);
        return new PaymentReference(payment.Id, payment.ProviderTxnId);
    }

    private static Dictionary<string, string> CreatePaidNotification(
        string providerTransactionId,
        long amount) =>
        new(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TmnCode"] = "TEST01",
            ["vnp_TxnRef"] = providerTransactionId,
            ["vnp_TransactionNo"] = "123456789",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_Version"] = "2.1.0"
        };

    private static void SignNotification(Dictionary<string, string> fields)
    {
        var payload = string.Join("&", new SortedDictionary<string, string>(fields, StringComparer.Ordinal)
            .Select(field => $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(field.Value)}"));
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(PaymentTestFactory.HashSecret));
        fields["vnp_SecureHash"] = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static async Task<HttpResponseMessage> SendIpnAsync(
        HttpClient client,
        Dictionary<string, string> fields)
    {
        var query = string.Join("&", fields.Select(field =>
            $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}"));
        return await client.GetAsync($"/api/payments/vnpay/ipn?{query}");
    }

    private static async Task<HttpResponseMessage> PostWithAntiforgeryAsync<T>(
        HttpClient client,
        string path,
        T body)
    {
        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            await GetAntiforgeryTokenAsync(client));
        var response = await client.PostAsJsonAsync(path, body);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        return response;
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "TestPassword!2026",
            rememberMe = false
        });
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/auth/antiforgery");
        Assert.NotNull(payload);
        return payload.RequestToken;
    }

    private static byte[] CreatePcmWav()
    {
        const int sampleRate = 8000;
        const int sampleCount = sampleRate;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + sampleCount);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate);
        writer.Write((short)1);
        writer.Write((short)8);
        writer.Write("data"u8);
        writer.Write(sampleCount);
        writer.Write(new byte[sampleCount]);
        writer.Flush();
        return stream.ToArray();
    }

    private sealed record SeededOrder(
        SubscriptionOrder Order,
        Location? Location,
        string? AudioUrl);

    private sealed record AntiforgeryResponse(string RequestToken);

    private sealed record PaymentLinkResponse(Guid PaymentId, string PaymentUrl);
    private sealed record PaymentReference(Guid Id, string ProviderTxnId);

    private sealed class PaymentTestFactory : WebApplicationFactory<Program>
    {
        public const string HashSecret = "Test-Only-VNPay-Hash-Secret";
        private readonly string _databaseName = $"payment-test-{Guid.NewGuid():N}";

        public PaymentTestFactory()
        {
            AudioDirectory = Path.Combine(Path.GetTempPath(), $"payment-test-audio-{Guid.NewGuid():N}");
            Directory.CreateDirectory(AudioDirectory);
        }

        public string AudioDirectory { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=localhost;Database=QOL;User Id=test;Password=test");
            builder.UseSetting("Storage:AudioDirectory", AudioDirectory);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=localhost;Database=QOL;User Id=test;Password=test",
                    ["Storage:AudioDirectory"] = AudioDirectory,
                    ["VnPay:Enabled"] = "true",
                    ["VnPay:TmnCode"] = "TEST01",
                    ["VnPay:HashSecret"] = HashSecret,
                    ["VnPay:PaymentUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                    ["VnPay:ReturnUrl"] = "http://localhost/payment/result",
                    ["VnPay:IpnUrl"] = "http://localhost/api/payments/vnpay/ipn"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<QolDbContext>();
                services.RemoveAll<DbContextOptions<QolDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<QolDbContext>>();
                services.AddDbContext<QolDbContext>(
                    options => options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<IPaymentGateway>();
                services.Configure<VnPayOptions>(options =>
                {
                    options.Enabled = true;
                    options.TmnCode = "TEST01";
                    options.HashSecret = HashSecret;
                    options.PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
                    options.ReturnUrl = "http://localhost/payment/result";
                    options.IpnUrl = "http://localhost/api/payments/vnpay/ipn";
                });
                services.AddScoped<IPaymentGateway, VnPayGateway>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(AudioDirectory))
            {
                Directory.Delete(AudioDirectory, recursive: true);
            }
        }
    }
}
