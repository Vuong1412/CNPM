using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace QOL.Tests.Domain;

public sealed class ModuleEntityTests
{
    [Fact]
    public void Language_NormalizesCodeAndPreservesName()
    {
        var language = new Language(" VI ", "Vietnamese", "Tiếng Việt");

        Assert.Equal("vi", language.Code);
        Assert.Equal("Vietnamese", language.Name);
        Assert.Equal("Tiếng Việt", language.NativeName);
        Assert.True(language.IsEnabled);
    }

    [Fact]
    public void User_NormalizesEmailAndSupportsAccountProfileAndRoleChanges()
    {
        var user = new User("  PERSON@EXAMPLE.COM ", "Traveler");

        Assert.Equal("person@example.com", user.Email);
        Assert.Equal("User", user.Role);

        user.SetPasswordHash("hashed-password");
        user.UpdateProfile("  Local guide  ");
        user.AssignAdministratorRole();

        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Equal("Local guide", user.DisplayName);
        Assert.Equal("Admin", user.Role);
    }

    [Fact]
    public void Location_CanBeUpdatedAndSoftDeleted()
    {
        var location = new Location("Old name", "Old address");

        location.UpdateDetails("  New name  ", "  New address  ");
        location.Deactivate();

        Assert.Equal("New name", location.Name);
        Assert.Equal("New address", location.Address);
        Assert.False(location.IsActive);
    }

    [Fact]
    public void AudioFile_StoresTranslatedContent()
    {
        var audio = new AudioFile(Guid.NewGuid(), Guid.NewGuid(), "guide.mp3", 90);

        Assert.True(audio.IsActive);
        audio.UpdateContent("  Welcome  ", "  A short translated introduction.  ");
        audio.SetActive(false);

        Assert.Equal("Welcome", audio.Title);
        Assert.Equal("A short translated introduction.", audio.Transcript);
        Assert.False(audio.IsActive);
        Assert.Throws<ArgumentException>(() => audio.UpdateContent("", "Transcript"));
    }

    [Fact]
    public void SubscriptionOrder_RemainsPendingUntilManuallyReviewed()
    {
        var plan = new SubscriptionPlan(
            "Monthly",
            "Access to audio tours",
            99000,
            "vnd",
            30);
        var order = new SubscriptionOrder(Guid.NewGuid(), plan);

        Assert.Equal(SubscriptionOrderStatus.Pending, order.Status);
        Assert.Equal(99000m, order.Amount);
        Assert.Equal("VND", order.Currency);
        Assert.Null(order.ReviewedAt);

        order.Review(SubscriptionOrderStatus.Verified, Guid.NewGuid(), "  Payment confirmed  ");

        Assert.Equal(SubscriptionOrderStatus.Verified, order.Status);
        Assert.Equal("Payment confirmed", order.ReviewNote);
        Assert.Throws<InvalidOperationException>(
            () => order.Review(SubscriptionOrderStatus.Rejected, Guid.NewGuid(), null));
    }

    [Fact]
    public void LocationReviewValidatesRatingAndSupportsModeration()
    {
        var review = new LocationReview(Guid.NewGuid(), Guid.NewGuid(), 5, "  Great tour  ");

        review.SetVisibility(false);

        Assert.Equal(5, review.Rating);
        Assert.Equal("Great tour", review.Comment);
        Assert.False(review.IsVisible);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => review.UpdateContent(0, "Invalid rating"));
    }

    [Fact]
    public void Favorite_RejectsMissingUser()
    {
        Assert.Throws<ArgumentException>(() => new Favorite(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void ListeningHistory_TracksListeningProgressAfterCreation()
    {
        var history = new ListeningHistory(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal(0, history.DurationSeconds);
        Assert.False(history.IsCompleted);

        history.UpdateProgress(45, false);

        Assert.Equal(45, history.DurationSeconds);
        Assert.False(history.IsCompleted);

        history.UpdateProgress(120, true);

        Assert.Equal(120, history.DurationSeconds);
        Assert.True(history.IsCompleted);
    }

    [Fact]
    public void ListeningHistory_RejectsNegativeProgress()
    {
        var history = new ListeningHistory(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => history.UpdateProgress(-1, false));
    }

    [Fact]
    public void UserSubscription_ExpiresAfterPlanDuration()
    {
        var subscription = new UserSubscription(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Monthly  ",
            " vnd ",
            99000,
            30);

        Assert.True(subscription.IsActive);
        Assert.Equal("Monthly", subscription.PlanName);
        Assert.Equal("VND", subscription.Currency);
        Assert.True(subscription.EndsAt > subscription.StartsAt);

        subscription.ExpireIfNeeded(subscription.EndsAt);
        Assert.False(subscription.IsActive);
    }

    [Fact]
    public void PersistenceModel_ContainsRequiredForeignKeysAndLanguageSeeds()
    {
        var options = new DbContextOptionsBuilder<QolDbContext>()
            .UseSqlServer("Server=localhost;Database=QOL;User Id=unused;Password=unused;TrustServerCertificate=True")
            .Options;
        using var dbContext = new QolDbContext(options);

        var model = dbContext.Model;
        var favoriteForeignKeys = model.FindEntityType(typeof(Favorite))!.GetForeignKeys();
        var historyForeignKeys = model.FindEntityType(typeof(ListeningHistory))!.GetForeignKeys();
        var languageSeedData = dbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(Language))!
            .GetSeedData();

        Assert.Contains(favoriteForeignKeys, key => key.PrincipalEntityType.ClrType == typeof(User));
        Assert.Contains(favoriteForeignKeys, key => key.PrincipalEntityType.ClrType == typeof(Location));
        Assert.Contains(historyForeignKeys, key => key.PrincipalEntityType.ClrType == typeof(User));
        Assert.Contains(historyForeignKeys, key => key.PrincipalEntityType.ClrType == typeof(Location));
        Assert.Contains(historyForeignKeys, key => key.PrincipalEntityType.ClrType == typeof(AudioFile));
        Assert.Contains(historyForeignKeys, key => key.PrincipalEntityType.ClrType == typeof(Language));
        Assert.Equal(5, languageSeedData.Count());
    }
}
