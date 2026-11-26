using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QOL.Application.Audio;
using QOL.Domain.Entities;
using QOL.Infrastructure.Audio;
using QOL.Infrastructure.Persistence;
using Xunit;

namespace QOL.Tests.Api;

public sealed class TranslationApiTests
{
    [Fact]
    public async Task TtsAudioUsesProtectedMediaAndTranslationUpdatesPreserveHistory()
    {
        using var factory = new TranslationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost")
        });
        var (admin, location, language, translation) = await SeedAdminAndTranslationAsync(factory);
        await SignInAsync(client, admin.Email);

        var created = await PostWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{location.Id}/translations/{translation.Id}/audio/tts");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var generated = await created.Content.ReadFromJsonAsync<AudioResponse>();
        Assert.NotNull(generated);
        Assert.Equal("TTS", generated.Source);
        Assert.Equal(1, generated.DurationSeconds);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(generated.AudioUrl)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var storedAudio = await db.AudioFiles.SingleAsync(audio => audio.Id == generated.Id);
            Assert.Equal(AudioSource.Tts, storedAudio.Source);
            Assert.Equal(translation.Id, storedAudio.TranslationId);
            Assert.Contains(
                await db.ActionLogs.Select(log => log.Action).ToListAsync(),
                action => action == "audio.tts-create");
        }

        var updateWithoutHistory = await PutWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{location.Id}/translations/{language.Id}",
            new { text = "Updated description without a listening history." });
        Assert.Equal(HttpStatusCode.OK, updateWithoutHistory.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            Assert.False(await db.AudioFiles.AnyAsync(audio => audio.Id == generated.Id));
            Assert.Contains(
                await db.ActionLogs.Select(log => log.Action).ToListAsync(),
                action => action == "audio.translation-delete");
        }

        var secondAudioResponse = await PostWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{location.Id}/translations/{translation.Id}/audio/tts");
        Assert.Equal(HttpStatusCode.Created, secondAudioResponse.StatusCode);
        var secondAudio = await secondAudioResponse.Content.ReadFromJsonAsync<AudioResponse>();
        Assert.NotNull(secondAudio);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            db.ListeningHistory.Add(new ListeningHistory(
                admin.Id,
                location.Id,
                secondAudio.Id,
                language.Id));
            await db.SaveChangesAsync();
        }

        var updateWithHistory = await PutWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{location.Id}/translations/{language.Id}",
            new { text = "Updated description with a listening history." });
        Assert.Equal(HttpStatusCode.OK, updateWithHistory.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            var preserved = await db.AudioFiles.SingleAsync(audio => audio.Id == secondAudio.Id);
            Assert.False(preserved.IsActive);
            Assert.True(File.Exists(Path.Combine(factory.AudioDirectory, Path.GetFileName(preserved.AudioUrl))));
            Assert.Contains(
                await db.ActionLogs.Select(log => log.Action).ToListAsync(),
                action => action == "audio.translation-hidden");
        }
    }

    private static async Task<(User Admin, Location Location, Language Language, LocationTranslation Translation)>
        SeedAdminAndTranslationAsync(TranslationTestFactory factory)
    {
        var admin = new User("tts-admin@example.com", "TTS Admin");
        admin.AssignAdministratorRole();
        var location = new Location("TTS destination", "Test address");
        var language = new Language("vi", "Vietnamese", "Tiếng Việt");
        var translation = new LocationTranslation(location.Id, language.Id, "Initial visitor description.");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        admin.SetPasswordHash(hasher.HashPassword(admin, "TestPassword!2026"));
        db.AddRange(admin, location, language, translation);
        await db.SaveChangesAsync();
        return (admin, location, language, translation);
    }

    private static async Task<HttpResponseMessage> PostWithAntiforgeryAsync(
        HttpClient client,
        string path)
    {
        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            await GetAntiforgeryTokenAsync(client));
        var response = await client.PostAsync(path, content: null);
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

    private static async Task SignInAsync(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            await GetAntiforgeryTokenAsync(client));
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

    private sealed record AntiforgeryResponse(string RequestToken);

    private sealed record AudioResponse(
        Guid Id,
        Guid LocationId,
        Guid LanguageId,
        string LanguageCode,
        string LanguageName,
        string LanguageNativeName,
        string Title,
        string Transcript,
        string AudioUrl,
        int DurationSeconds,
        bool IsActive,
        string Source,
        Guid? TranslationId);

    private sealed class TranslationTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"translation-test-{Guid.NewGuid():N}";

        public TranslationTestFactory()
        {
            AudioDirectory = Path.Combine(Path.GetTempPath(), $"translation-test-audio-{Guid.NewGuid():N}");
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
                    ["Storage:AudioDirectory"] = AudioDirectory
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<QolDbContext>();
                services.RemoveAll<DbContextOptions<QolDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<QolDbContext>>();
                services.AddDbContext<QolDbContext>(
                    options => options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<ITextToSpeechProvider>();
                services.AddSingleton<ITextToSpeechProvider, FakeTextToSpeechProvider>();
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
