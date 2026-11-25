using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class LocationImageConfiguration : IEntityTypeConfiguration<LocationImage>
{
    public void Configure(EntityTypeBuilder<LocationImage> builder)
    {
        builder.HasKey(image => image.Id);
        builder.Property(image => image.ImageUrl).HasMaxLength(2048).IsRequired();
        builder.Property(image => image.AltText).HasMaxLength(500);
        builder.Property(image => image.CreatedAt).HasColumnType("datetimeoffset(7)");
        builder.HasIndex(image => new { image.LocationId, image.DisplayOrder });
    }
}
