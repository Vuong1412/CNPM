using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using QOL.API.Endpoints;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace QOL.Tests.Api;

public sealed class AudioAccessApiTests
{
    [Fact]
    public async Task HiddenAudioCannotBeDownloadedButActiveAudioCan()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: false);
        await SeedUserAsync(factory, isAdmin: true);
        var fileName = Path.GetFileName(audio.AudioUrl);
        await File.WriteAllBytesAsync(Path.Combine(factory.AudioDirectory, fileName), [1, 2, 3]);

        var hiddenResponse = await client.GetAsync(audio.AudioUrl);

        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
        await SignInAsync(client);
        var adminPreviewResponse = await client.GetAsync(audio.AudioUrl);
        Assert.Equal(HttpStatusCode.OK, adminPreviewResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var storedAudio = await db.AudioFiles.SingleAsync(item => item.Id == audio.Id);
            storedAudio.SetActive(true);
            await db.SaveChangesAsync();
        }

        var activeResponse = await client.GetAsync(audio.AudioUrl);
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, await activeResponse.Content.ReadAsByteArrayAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var location = await db.Locations.SingleAsync(item => item.Id == audio.LocationId);
            location.Deactivate();
            await db.SaveChangesAsync();
        }

        using var guestClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await guestClient.GetAsync(audio.AudioUrl)).StatusCode);
    }

    [Fact]
    public async Task LockedUserCannotReuseExistingCookieOnProtectedMeEndpoint()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory);
        await SignInAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var storedUser = await db.Users.SingleAsync(item => item.Id == user.Id);
            storedUser.SetActive(false);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/me/favorites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicAudioRequiresAnActiveSubscription()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory);
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: true);
        var fileName = Path.GetFileName(audio.AudioUrl);
        await File.WriteAllBytesAsync(Path.Combine(factory.AudioDirectory, fileName), [1, 2, 3]);
        await SignInAsync(client);

        var withoutSubscription = await client.GetAsync($"/api/locations/{audio.LocationId}/audio");
        Assert.Equal(HttpStatusCode.Forbidden, withoutSubscription.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(audio.AudioUrl)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var plan = new SubscriptionPlan("Test", "Test plan", 1, "USD", 30);
            var order = new SubscriptionOrder(user.Id, plan);
            db.SubscriptionPlans.Add(plan);
            db.SubscriptionOrders.Add(order);
            db.UserSubscriptions.Add(new UserSubscription(
                user.Id,
                plan.Id,
                order.Id,
                plan.Name,
                plan.Currency,
                plan.Price,
                plan.DurationDays));
            await db.SaveChangesAsync();
        }

        var withSubscription = await client.GetAsync($"/api/locations/{audio.LocationId}/audio");
        Assert.Equal(HttpStatusCode.OK, withSubscription.StatusCode);
        var audioItems = await withSubscription.Content.ReadFromJsonAsync<AudioResponse[]>();
        Assert.Contains(audioItems!, item => item.Id == audio.Id);
        var mediaResponse = await client.GetAsync(audio.AudioUrl);
        Assert.Equal(HttpStatusCode.OK, mediaResponse.StatusCode);
    }

    [Fact]
    public async Task UploadUsesWavHeaderInsteadOfClientDuration()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        var (location, language, _) = await SeedAudioAsync(factory, isActive: true);
        await SignInAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("WAV duration test"), "Title");
        form.Add(new StringContent("Generated test audio"), "Transcript");
        form.Add(new StringContent(language.Id.ToString()), "LanguageId");
        form.Add(new StringContent("900"), "DurationSeconds");
        form.Add(new ByteArrayContent(CreatePcmWav(2)), "File", "duration-test.wav");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var response = await client.PostAsync(
            $"/api/admin/locations/{location.Id}/audio",
            form);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdAudio = await response.Content.ReadFromJsonAsync<AudioResponse>();
        Assert.NotNull(createdAudio);
        Assert.Equal(2, createdAudio.DurationSeconds);
    }

    private static async Task<User> SeedUserAsync(ApiTestFactory factory, bool isAdmin = false)
    {
        var user = new User("test@example.com", "Test User");
        if (isAdmin)
        {
            user.AssignAdministratorRole();
        }
        user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, "TestPassword!2026"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Location Location, Language Language, AudioFile Audio)> SeedAudioAsync(
        ApiTestFactory factory,
        bool isActive)
    {
        var location = new Location("Test destination", "Test address");
        var language = new Language("zz", "Test language", "Test language");
        var audio = new AudioFile(
            location.Id,
            language.Id,
            "Test guide",
            "Test transcript",
            $"/media/audio/{Guid.NewGuid():N}.wav",
            3);
        audio.SetActive(isActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        db.Locations.Add(location);
        db.Languages.Add(language);
        db.AudioFiles.Add(audio);
        await db.SaveChangesAsync();
        return (location, language, audio);
    }

    private static async Task SignInAsync(HttpClient client)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test@example.com",
            password = "TestPassword!2026",
            rememberMe = false
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var antiforgery = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/auth/antiforgery");
        Assert.NotNull(antiforgery);
        return antiforgery.RequestToken;
    }

    private static byte[] CreatePcmWav(int durationSeconds)
    {
        const int sampleRate = 8000;
        const short bitsPerSample = 8;
        var sampleCount = sampleRate * durationSeconds;
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
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(sampleCount);
        writer.Write(new byte[sampleCount]);
        writer.Flush();
        return stream.ToArray();
    }

    private sealed record AntiforgeryResponse(string RequestToken);

    private sealed class ApiTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"qol-test-{Guid.NewGuid():N}";

        public ApiTestFactory()
        {
            AudioDirectory = Path.Combine(Path.GetTempPath(), $"qol-test-audio-{Guid.NewGuid():N}");
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
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=QOL;User Id=test;Password=test",
                    ["Storage:AudioDirectory"] = AudioDirectory
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<QolDbContext>();
                services.RemoveAll<DbContextOptions<QolDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<QolDbContext>>();
                services.AddDbContext<QolDbContext>(options => options.UseInMemoryDatabase(_databaseName));
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
