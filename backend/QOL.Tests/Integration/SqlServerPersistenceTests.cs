using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QOL.Application.Commerce;
using QOL.Domain.Entities;
using QOL.Infrastructure.Notifications;
using QOL.Infrastructure.Persistence;
using QOL.Infrastructure.Persistence.Services;
using Xunit;

namespace QOL.Tests.Integration;

public sealed class SqlServerPersistenceTests
{
    [SqlServerFact]
    public async Task SqlServerEnforcesPaymentForeignKeyAndUniqueProviderTransaction()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var db = database.Context;

        db.Payments.Add(new Payment(Guid.NewGuid(), "VNPAY", "missing-order", 100, "VND"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var user = new User("buyer@example.com", "Buyer");
        var plan = new SubscriptionPlan("Monthly", "One month", 100, "VND", 30);
        var order = new SubscriptionOrder(user.Id, plan);
        db.AddRange(user, plan, order);
        await db.SaveChangesAsync();

        db.Payments.Add(new Payment(order.Id, "VNPAY", "provider-txn-1", 100, "VND"));
        await db.SaveChangesAsync();
        db.Payments.Add(new Payment(order.Id, "VNPAY", "provider-txn-1", 100, "VND"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Single(await db.Payments.AsNoTracking().ToListAsync());
    }

    [SqlServerFact]
    public async Task SerializableFulfillmentCreatesOnlyOneSubscriptionForConcurrentVerification()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var user = new User("buyer@example.com", "Buyer");
        var plan = new SubscriptionPlan("Monthly", "One month", 100, "VND", 30);
        var order = new SubscriptionOrder(user.Id, plan);
        database.Context.AddRange(user, plan, order);
        await database.Context.SaveChangesAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstService = CreateFulfillmentService(firstContext);
        var secondService = CreateFulfillmentService(secondContext);

        var results = await Task.WhenAll(
            firstService.VerifyAsync(order.Id, null, "first", CancellationToken.None),
            secondService.VerifyAsync(order.Id, null, "second", CancellationToken.None));

        Assert.Contains(OrderFulfillmentResult.Fulfilled, results);
        Assert.Contains(OrderFulfillmentResult.AlreadyFulfilled, results);
        Assert.Single(await database.Context.UserSubscriptions.AsNoTracking().ToListAsync());
    }

    private static OrderFulfillmentService CreateFulfillmentService(QolDbContext db) =>
        new(
            db,
            new FakeEmailProvider(NullLogger<FakeEmailProvider>.Instance),
            NullLogger<OrderFulfillmentService>.Instance);
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QOL_TEST_SQLSERVER")))
        {
            Skip = "Set QOL_TEST_SQLSERVER to run SQL Server integration tests.";
        }
    }
}

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private readonly string _connectionString;

    private SqlServerTestDatabase(string connectionString)
    {
        _connectionString = connectionString;
        Context = CreateContext();
    }

    public QolDbContext Context { get; }

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable("QOL_TEST_SQLSERVER")
            ?? throw new InvalidOperationException("QOL_TEST_SQLSERVER is not configured.");
        var builder = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = $"QOL_Test_{Guid.NewGuid():N}"
        };
        var database = new SqlServerTestDatabase(builder.ConnectionString);
        await database.Context.Database.EnsureCreatedAsync();
        return database;
    }

    public QolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<QolDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new QolDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}
