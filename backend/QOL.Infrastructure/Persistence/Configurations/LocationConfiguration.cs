using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Name).HasMaxLength(200).IsRequired();
        builder.Property(location => location.Address).HasMaxLength(500).IsRequired();
        builder.Property(location => location.CreatedAt).HasColumnType("datetimeoffset(7)");
    }
}
