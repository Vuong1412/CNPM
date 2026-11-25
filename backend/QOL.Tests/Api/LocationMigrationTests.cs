using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QOL.Infrastructure.Persistence;
using Xunit;

namespace QOL.Tests.Api;

public sealed class LocationMigrationTests
{
    [Fact]
    public void LocationMigrationCopiesLegacyActivityBeforeDroppingItsColumn()
    {
        var options = new DbContextOptionsBuilder<QolDbContext>()
            .UseSqlServer("Server=localhost;Database=QOL;User Id=test;Password=test")
            .Options;
        using var db = new QolDbContext(options);
        var migrator = db.GetService<IMigrator>();

        var upScript = migrator.GenerateScript(
            "20261118175111_AddUserSubscriptions",
            "20261125162055_AddLocationMetadata");
        var copyIndex = upScript.IndexOf("UPDATE [Locations]", StringComparison.Ordinal);
        var dropIndex = upScript.IndexOf("DROP COLUMN [IsActive]", StringComparison.Ordinal);

        Assert.Contains(
            "CASE WHEN [IsActive] = 1 THEN N'ACTIVE' ELSE N'INACTIVE' END",
            upScript);
        Assert.True(copyIndex >= 0 && dropIndex > copyIndex);

        var downScript = migrator.GenerateScript(
            "20261125162055_AddLocationMetadata",
            "20261118175111_AddUserSubscriptions");
        var restoreIndex = downScript.IndexOf("UPDATE [Locations]", StringComparison.Ordinal);
        var dropStatusIndex = downScript.IndexOf("DROP COLUMN [Status]", StringComparison.Ordinal);
        Assert.Contains(
            "CASE WHEN [Status] = N'ACTIVE' THEN 1 ELSE 0 END",
            downScript);
        Assert.True(restoreIndex >= 0 && dropStatusIndex > restoreIndex);
    }
}
