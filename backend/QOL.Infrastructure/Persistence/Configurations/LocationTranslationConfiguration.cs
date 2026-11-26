using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class LocationTranslationConfiguration : IEntityTypeConfiguration<LocationTranslation>
{
    public void Configure(EntityTypeBuilder<LocationTranslation> builder)
    {
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Text).IsRequired();
        builder.Property(translation => translation.UpdatedAt).HasColumnType("datetimeoffset(7)");
        builder.HasOne(translation => translation.Location)
            .WithMany()
            .HasForeignKey(translation => translation.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(translation => translation.Language)
            .WithMany()
            .HasForeignKey(translation => translation.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(translation => new { translation.LocationId, translation.LanguageId })
            .IsUnique();
    }
}
