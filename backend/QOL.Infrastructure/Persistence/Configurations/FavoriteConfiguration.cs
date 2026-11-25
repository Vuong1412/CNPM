using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.HasKey(favorite => new { favorite.UserId, favorite.LocationId });
        builder.Property(favorite => favorite.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(favorite => favorite.User)
            .WithMany(user => user.Favorites)
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(favorite => favorite.Location)
            .WithMany(location => location.Favorites)
            .HasForeignKey(favorite => favorite.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(favorite => favorite.LocationId);
    }
}
