using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QOL.Infrastructure.Persistence;

public sealed class QolDbContextFactory : IDesignTimeDbContextFactory<QolDbContext>
{
    public QolDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=QOL;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<QolDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new QolDbContext(options);
    }
}
