using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;
using Xunit;

namespace QOL.Tests.Api;

public sealed class VisitorAccessApiTests
{
    [Fact]
    public async Task VisitorCanStartSessionAndSetLanguageWithoutGettingAccountAccess()
    {
        using var factory = new VisitorTestFactory();
        using var client = factory.CreateClient();
        var language = new Language("vi", "Vietnamese", "Tiếng Việt");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            db.Languages.Add(language);
            await db.SaveChangesAsync();
        }

        var deviceId = Guid.NewGuid();
        var startResponse = await PostWithAntiforgeryAsync(
            client,
            "/api/access/start",
            new { deviceId = deviceId.ToString("D") });

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.Contains(
            startResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("QOL.Visitor=", StringComparison.Ordinal) &&
                cookie.Contains("Path=/api", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        var languageResponse = await PutWithAntiforgeryAsync(
            client,
            "/api/session/language",
            new { languageCode = "vi" });
        Assert.Equal(HttpStatusCode.OK, languageResponse.StatusCode);

        var accountResponse = await client.GetAsync("/api/me/favorites");
        Assert.Equal(HttpStatusCode.Unauthorized, accountResponse.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QolDbContext>();
        var savedSession = await verifyDb.VisitorSessions
            .Include(session => session.Language)
            .SingleAsync(session => session.DeviceId == deviceId);
        Assert.Equal(language.Id, savedSession.LanguageId);
    }

    [Fact]
    public async Task VisitorSessionDoesNotBypassSubscriptionAudioAccess()
    {
        using var factory = new VisitorTestFactory();
        using var client = factory.CreateClient();
        var location = new Location("Visitor test", "Test address");
        var language = new Language("en", "English", "English");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            db.Locations.Add(location);
            db.Languages.Add(language);
            await db.SaveChangesAsync();
        }

        var start = await PostWithAntiforgeryAsync(
            client,
            "/api/access/start",
            new { deviceId = Guid.NewGuid().ToString("D") });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var audioResponse = await client.GetAsync($"/api/locations/{location.Id}/audio");
        Assert.Equal(HttpStatusCode.PaymentRequired, audioResponse.StatusCode);
        using var problem = JsonDocument.Parse(await audioResponse.Content.ReadAsStringAsync());
        Assert.Equal("subscription-required", problem.RootElement.GetProperty("type").GetString());

        var policy = await client.GetFromJsonAsync<JsonElement>("/api/policy");
        Assert.False(policy.GetProperty("visitorSessionGrantsAudioAccess").GetBoolean());
        Assert.True(policy.GetProperty("audioPlaybackRequiresActiveSubscription").GetBoolean());
    }

    [Fact]
    public async Task VisitorLanguageCannotBeChangedWithoutVisitorCookie()
    {
        using var factory = new VisitorTestFactory();
        using var client = factory.CreateClient();

        var response = await PutWithAntiforgeryAsync(
            client,
            "/api/session/language",
            new { languageCode = "vi" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private static async Task<HttpResponseMessage> PutWithAntiforgeryAsync<T>(
        HttpClient client,
        string path,
        T body)
    {
        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            await GetAntiforgeryTokenAsync(client));
        var response = await client.PutAsJsonAsync(path, body);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        return response;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/auth/antiforgery");
        Assert.NotNull(payload);
        return payload.RequestToken;
    }

    private sealed record AntiforgeryResponse(string RequestToken);

    private sealed class VisitorTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"visitor-test-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=localhost;Database=QOL;User Id=test;Password=test");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=localhost;Database=QOL;User Id=test;Password=test"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<QolDbContext>();
                services.RemoveAll<DbContextOptions<QolDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<QolDbContext>>();
                services.AddDbContext<QolDbContext>(
                    options => options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
