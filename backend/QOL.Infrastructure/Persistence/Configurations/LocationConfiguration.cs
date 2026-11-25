using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Name).HasMaxLength(200).IsRequired();
        builder.Property(location => location.Address).HasMaxLength(500).IsRequired();
        builder.Property(location => location.Latitude).HasPrecision(9, 6);
        builder.Property(location => location.Longitude).HasPrecision(9, 6);
        builder.Property(location => location.RadiusMeters).HasDefaultValue(100);
        builder.Property(location => location.Priority).HasDefaultValue(0);
        builder.Property(location => location.OriginalDescription).HasMaxLength(4000);
        builder.Property(location => location.DirectionLink).HasMaxLength(2048);
        builder.Property(location => location.Status)
            .HasConversion(new ValueConverter<LocationStatus, string>(
                status => status.ToString().ToUpperInvariant(),
                status => Enum.Parse<LocationStatus>(status, ignoreCase: true)))
            .HasMaxLength(16)
            .HasDefaultValueSql("N'ACTIVE'");
        builder.Property(location => location.CreatedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(location => location.UpdatedAt)
            .HasColumnType("datetimeoffset(7)")
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.HasMany(location => location.Images)
            .WithOne(image => image.Location)
            .HasForeignKey(image => image.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
