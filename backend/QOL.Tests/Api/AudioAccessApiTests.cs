using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task MigratedActiveLocationIsVisibleWithLocationMetadata()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var location = new Location(
            "Legacy active destination",
            "Legacy address",
            latitude: 15.8801m,
            longitude: 108.338m,
            radiusMeters: 250,
            originalDescription: "A legacy destination description");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            db.Locations.Add(location);
            db.Locations.Add(new Location("Legacy inactive destination", "Hidden address", status: LocationStatus.Inactive));
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/locations/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var locations = payload.RootElement;
        var item = Assert.Single(locations.EnumerateArray());
        Assert.Equal(location.Id, item.GetProperty("id").GetGuid());
        Assert.Equal("ACTIVE", item.GetProperty("status").GetString());
        Assert.Equal(15.8801m, item.GetProperty("latitude").GetDecimal());
        Assert.Equal(108.338m, item.GetProperty("longitude").GetDecimal());
        Assert.Equal(250, item.GetProperty("radiusMeters").GetInt32());
        Assert.Equal("A legacy destination description", item.GetProperty("originalDescription").GetString());
    }

    [Fact]
    public async Task AdminCanUploadAndDeleteSignedLocationImage()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        var (location, _, _) = await SeedAudioAsync(factory, isActive: true);
        await SignInAsync(client);
        var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(imageBytes), "File", "view.png");
        form.Add(new StringContent("Test view"), "AltText");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var upload = await client.PostAsync($"/api/admin/locations/{location.Id}/images", form);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        using var responseJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var imageUrl = responseJson.RootElement.GetProperty("imageUrl").GetString();
        Assert.NotNull(imageUrl);
        var imageResponse = await client.GetAsync(imageUrl);
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Equal(imageBytes, await imageResponse.Content.ReadAsByteArrayAsync());

        var imageId = responseJson.RootElement.GetProperty("id").GetGuid();
        var deletion = await DeleteWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{location.Id}/images/{imageId}");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(imageUrl)).StatusCode);
    }

    [Fact]
    public async Task LocationImageUploadRejectsMismatchedSignature()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        var (location, _, _) = await SeedAudioAsync(factory, isActive: true);
        await SignInAsync(client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0, 1, 2, 3, 4, 5, 6, 7]), "File", "not-a-png.png");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var response = await client.PostAsync($"/api/admin/locations/{location.Id}/images", form);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LocationCreateRejectsIncompleteCoordinatePairAsValidationProblem()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        await SignInAsync(client);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var response = await client.PostAsJsonAsync("/api/admin/locations", new
        {
            name = "Incomplete coordinates",
            address = "Test address",
            latitude = 15.88,
            status = "ACTIVE"
        });
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ListeningHistoryStoresTapAndAutomaticGpsTriggerMethods()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory);
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: true);
        await SignInAsync(client);

        var tapResponse = await StartListeningAsync(client, audio.Id);
        var gpsResponse = await StartListeningAsync(client, audio.Id, "AUTO_GPS");
        Assert.True(
            tapResponse.StatusCode == HttpStatusCode.Created,
            await tapResponse.Content.ReadAsStringAsync());
        Assert.True(
            gpsResponse.StatusCode == HttpStatusCode.Created,
            await gpsResponse.Content.ReadAsStringAsync());

        using var historyPayload = JsonDocument.Parse(
            await (await client.GetAsync("/api/me/listening-history")).Content.ReadAsStringAsync());
        var savedTriggers = historyPayload.RootElement
            .EnumerateArray()
            .Select(item => item.GetProperty("triggerMethod").GetString()
                ?? throw new InvalidOperationException("Listening history trigger method was missing."))
            .Order()
            .ToArray();
        Assert.Equal(["AUTO_GPS", "TAP"], savedTriggers);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        var methods = await db.ListeningHistory
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.ListenedAt)
            .Select(item => item.TriggerMethod)
            .ToArrayAsync();
        Assert.Equal(
            [ListeningTriggerMethod.Tap, ListeningTriggerMethod.AutoGps],
            methods);
    }

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
    public async Task UserWithoutSubscriptionGetsPaymentRequiredWithAvailablePlans()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory);
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: true);
        await SeedPlanAsync(factory);
        var fileName = Path.GetFileName(audio.AudioUrl);
        await File.WriteAllBytesAsync(Path.Combine(factory.AudioDirectory, fileName), [1, 2, 3]);
        await SignInAsync(client);

        var withoutSubscription = await client.GetAsync($"/api/locations/{audio.LocationId}/audio");
        await AssertSubscriptionRequiredAsync(withoutSubscription, requiresAuthentication: false);
        await AssertSubscriptionRequiredAsync(
            await client.GetAsync(audio.AudioUrl),
            requiresAuthentication: false);

        using var anonymousClient = factory.CreateClient();
        await AssertSubscriptionRequiredAsync(
            await anonymousClient.GetAsync($"/api/locations/{audio.LocationId}/audio"),
            requiresAuthentication: true);
        await AssertSubscriptionRequiredAsync(
            await anonymousClient.GetAsync(audio.AudioUrl),
            requiresAuthentication: true);
    }

    [Fact]
    public async Task UserWithActiveSubscriptionCanListAndStreamAudio()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory);
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: true);
        await SeedSubscriptionAsync(factory, user.Id);
        var fileName = Path.GetFileName(audio.AudioUrl);
        await File.WriteAllBytesAsync(Path.Combine(factory.AudioDirectory, fileName), [1, 2, 3]);
        await SignInAsync(client);

        var withSubscription = await client.GetAsync($"/api/locations/{audio.LocationId}/audio");
        Assert.Equal(HttpStatusCode.OK, withSubscription.StatusCode);
        var audioItems = await withSubscription.Content.ReadFromJsonAsync<AudioResponse[]>();
        Assert.Contains(audioItems!, item => item.Id == audio.Id);
        var mediaResponse = await client.GetAsync(audio.AudioUrl);
        Assert.Equal(HttpStatusCode.OK, mediaResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredSubscriptionIsDeactivatedAndCannotAccessAudio()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory);
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: true);
        var subscriptionId = await SeedSubscriptionAsync(factory, user.Id, isExpired: true);
        await SeedPlanAsync(factory);
        var fileName = Path.GetFileName(audio.AudioUrl);
        await File.WriteAllBytesAsync(Path.Combine(factory.AudioDirectory, fileName), [1, 2, 3]);
        await SignInAsync(client);

        await AssertSubscriptionRequiredAsync(
            await client.GetAsync($"/api/locations/{audio.LocationId}/audio"),
            requiresAuthentication: false);
        await AssertSubscriptionRequiredAsync(
            await client.GetAsync(audio.AudioUrl),
            requiresAuthentication: false);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        var storedSubscription = await db.UserSubscriptions.SingleAsync(item => item.Id == subscriptionId);
        Assert.False(storedSubscription.IsActive);
    }

    [Fact]
    public async Task AdminCanListAndStreamAudioWithoutSubscription()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        var (_, _, audio) = await SeedAudioAsync(factory, isActive: true);
        var fileName = Path.GetFileName(audio.AudioUrl);
        await File.WriteAllBytesAsync(Path.Combine(factory.AudioDirectory, fileName), [1, 2, 3]);
        await SignInAsync(client);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/locations/{audio.LocationId}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(audio.AudioUrl)).StatusCode);
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

    [Fact]
    public async Task DeleteRemovesAudioWithoutHistoryAndOnlyHidesAudioWithHistory()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var user = await SeedUserAsync(factory, isAdmin: true);
        var (locationWithoutHistory, _, audioWithoutHistory) = await SeedAudioAsync(factory, isActive: true);
        var (locationWithHistory, languageWithHistory, audioWithHistory) =
            await SeedAudioAsync(factory, isActive: true);
        var fileWithoutHistory = Path.Combine(factory.AudioDirectory, Path.GetFileName(audioWithoutHistory.AudioUrl));
        var fileWithHistory = Path.Combine(factory.AudioDirectory, Path.GetFileName(audioWithHistory.AudioUrl));
        await File.WriteAllBytesAsync(fileWithoutHistory, [1]);
        await File.WriteAllBytesAsync(fileWithHistory, [2]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
            db.ListeningHistory.Add(new ListeningHistory(
                user.Id,
                locationWithHistory.Id,
                audioWithHistory.Id,
                languageWithHistory.Id));
            await db.SaveChangesAsync();
        }

        await SignInAsync(client);
        var deleteWithoutHistory = await DeleteWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{locationWithoutHistory.Id}/audio/{audioWithoutHistory.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteWithoutHistory.StatusCode);
        Assert.False(File.Exists(fileWithoutHistory));

        var deleteWithHistory = await DeleteWithAntiforgeryAsync(
            client,
            $"/api/admin/locations/{locationWithHistory.Id}/audio/{audioWithHistory.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteWithHistory.StatusCode);
        Assert.True(File.Exists(fileWithHistory));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QolDbContext>();
        Assert.False(await verifyDb.AudioFiles.AnyAsync(item => item.Id == audioWithoutHistory.Id));
        Assert.False((await verifyDb.AudioFiles.SingleAsync(item => item.Id == audioWithHistory.Id)).IsActive);
        Assert.True(await verifyDb.ListeningHistory.AnyAsync(item => item.AudioId == audioWithHistory.Id));
    }

    [Fact]
    public async Task ReplaceAudioDeletesOldFileOnlyAfterNewFileIsSaved()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        var (location, _, audio) = await SeedAudioAsync(factory, isActive: true);
        var oldFilePath = Path.Combine(factory.AudioDirectory, Path.GetFileName(audio.AudioUrl));
        await File.WriteAllBytesAsync(oldFilePath, [1, 2, 3]);
        await SignInAsync(client);

        var replacementResponse = await ReplaceAudioAsync(
            client,
            location.Id,
            audio.Id,
            CreatePcmWav(2),
            "replacement.wav",
            900);
        Assert.Equal(HttpStatusCode.OK, replacementResponse.StatusCode);
        var replacement = await replacementResponse.Content.ReadFromJsonAsync<AudioResponse>();
        Assert.NotNull(replacement);
        Assert.Equal(2, replacement.DurationSeconds);
        Assert.False(File.Exists(oldFilePath));
        var replacementFilePath = Path.Combine(factory.AudioDirectory, Path.GetFileName(replacement.AudioUrl));
        Assert.True(File.Exists(replacementFilePath));

        var invalidReplacementResponse = await ReplaceAudioAsync(
            client,
            location.Id,
            audio.Id,
            [1, 2, 3],
            "invalid.wav",
            2);
        Assert.Equal(HttpStatusCode.BadRequest, invalidReplacementResponse.StatusCode);
        Assert.True(File.Exists(replacementFilePath));
    }

    [Fact]
    public async Task OrphanAudioCleanupSupportsDryRunAndDeletion()
    {
        using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        await SeedUserAsync(factory, isAdmin: true);
        await SignInAsync(client);
        var orphanPath = Path.Combine(factory.AudioDirectory, "orphan.wav");
        await File.WriteAllBytesAsync(orphanPath, [1, 2, 3]);

        var dryRun = await CleanupAudioAsync(client, dryRun: true);
        Assert.Equal(HttpStatusCode.OK, dryRun.StatusCode);
        Assert.True(File.Exists(orphanPath));
        var dryRunResult = await dryRun.Content.ReadFromJsonAsync<AudioOrphanCleanupResponse>();
        Assert.NotNull(dryRunResult);
        Assert.True(dryRunResult.DryRun);
        Assert.Equal(1, dryRunResult.Count);

        var cleanup = await CleanupAudioAsync(client, dryRun: false);
        Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
        Assert.False(File.Exists(orphanPath));
        var cleanupResult = await cleanup.Content.ReadFromJsonAsync<AudioOrphanCleanupResponse>();
        Assert.NotNull(cleanupResult);
        Assert.False(cleanupResult.DryRun);
        Assert.Equal(1, cleanupResult.Count);
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
        var languageCode = $"t{Guid.NewGuid():N}"[..10];
        var language = new Language(languageCode, "Test language", "Test language");
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

    private static async Task<Guid> SeedSubscriptionAsync(
        ApiTestFactory factory,
        Guid userId,
        bool isExpired = false)
    {
        var plan = new SubscriptionPlan("Test", "Test plan", 1, "USD", 30);
        var order = new SubscriptionOrder(userId, plan);
        var subscription = new UserSubscription(
            userId,
            plan.Id,
            order.Id,
            plan.Name,
            plan.Currency,
            plan.Price,
            plan.DurationDays);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        db.SubscriptionPlans.Add(plan);
        db.SubscriptionOrders.Add(order);
        db.UserSubscriptions.Add(subscription);
        await db.SaveChangesAsync();

        if (isExpired)
        {
            db.Entry(subscription).Property(item => item.EndsAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        return subscription.Id;
    }

    private static async Task SeedPlanAsync(ApiTestFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QolDbContext>();
        db.SubscriptionPlans.Add(new SubscriptionPlan("Available", "Available plan", 5, "USD", 30));
        await db.SaveChangesAsync();
    }

    private static async Task AssertSubscriptionRequiredAsync(
        HttpResponseMessage response,
        bool requiresAuthentication)
    {
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = problem.RootElement;
        Assert.Equal("subscription-required", root.GetProperty("type").GetString());
        Assert.Equal("subscription-required", root.GetProperty("code").GetString());
        Assert.Equal(requiresAuthentication, root.GetProperty("requiresAuthentication").GetBoolean());
        Assert.True(root.GetProperty("availablePlans").GetArrayLength() > 0);
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

    private static async Task<HttpResponseMessage> DeleteWithAntiforgeryAsync(
        HttpClient client,
        string path)
    {
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var response = await client.DeleteAsync(path);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        return response;
    }

    private static async Task<HttpResponseMessage> StartListeningAsync(
        HttpClient client,
        Guid audioId,
        string? triggerMethod = null)
    {
        var body = new Dictionary<string, object?> { ["audioId"] = audioId };
        if (triggerMethod is not null)
        {
            body["triggerMethod"] = triggerMethod;
        }

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var response = await client.PostAsJsonAsync("/api/me/listening-history", body);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        return response;
    }

    private static async Task<HttpResponseMessage> ReplaceAudioAsync(
        HttpClient client,
        Guid locationId,
        Guid audioId,
        byte[] fileBytes,
        string fileName,
        int durationSeconds)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(durationSeconds.ToString()), "DurationSeconds");
        form.Add(new ByteArrayContent(fileBytes), "File", fileName);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var response = await client.PutAsync(
            $"/api/admin/locations/{locationId}/audio/{audioId}/file",
            form);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        return response;
    }

    private static async Task<HttpResponseMessage> CleanupAudioAsync(HttpClient client, bool dryRun)
    {
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        var response = await client.PostAsJsonAsync(
            $"/api/admin/audio/cleanup?dryRun={dryRun.ToString().ToLowerInvariant()}",
            new { });
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        return response;
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
            LocationImageDirectory = Path.Combine(Path.GetTempPath(), $"qol-test-images-{Guid.NewGuid():N}");
        }

        public string AudioDirectory { get; }
        public string LocationImageDirectory { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=localhost;Database=QOL;User Id=test;Password=test");
            builder.UseSetting("Storage:AudioDirectory", AudioDirectory);
            builder.UseSetting("Storage:LocationImageDirectory", LocationImageDirectory);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=QOL;User Id=test;Password=test",
                    ["Storage:AudioDirectory"] = AudioDirectory,
                    ["Storage:LocationImageDirectory"] = LocationImageDirectory
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
            if (disposing && Directory.Exists(LocationImageDirectory))
            {
                Directory.Delete(LocationImageDirectory, recursive: true);
            }
        }
    }
}
