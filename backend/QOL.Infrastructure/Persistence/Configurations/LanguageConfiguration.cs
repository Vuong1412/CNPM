using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence.SeedData;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.HasKey(language => language.Id);
        builder.Property(language => language.Code).HasMaxLength(10).IsRequired();
        builder.Property(language => language.Name).HasMaxLength(100).IsRequired();
        builder.Property(language => language.NativeName).HasMaxLength(100).IsRequired();
        builder.HasIndex(language => language.Code).IsUnique();
        builder.HasData(LanguageSeedData.Languages);
    }
}
