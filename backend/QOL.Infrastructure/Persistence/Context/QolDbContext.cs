using Microsoft.EntityFrameworkCore;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence;

public sealed class QolDbContext(DbContextOptions<QolDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationImage> LocationImages => Set<LocationImage>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ListeningHistory> ListeningHistory => Set<ListeningHistory>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionOrder> SubscriptionOrders => Set<SubscriptionOrder>();
    public DbSet<LocationReview> LocationReviews => Set<LocationReview>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QolDbContext).Assembly);
    }
}
