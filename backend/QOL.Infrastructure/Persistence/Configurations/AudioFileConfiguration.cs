using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class AudioFileConfiguration : IEntityTypeConfiguration<AudioFile>
{
    public void Configure(EntityTypeBuilder<AudioFile> builder)
    {
        builder.HasKey(audio => audio.Id);
        builder.Property(audio => audio.Title).HasMaxLength(200).IsRequired();
        builder.Property(audio => audio.Transcript).IsRequired();
        builder.Property(audio => audio.AudioUrl).HasMaxLength(2048).IsRequired();
        builder.Property(audio => audio.Source)
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasDefaultValue(AudioSource.Upload);
        builder.HasOne(audio => audio.Translation)
            .WithMany(translation => translation.AudioFiles)
            .HasForeignKey(audio => audio.TranslationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(audio => audio.Location)
            .WithMany(location => location.AudioFiles)
            .HasForeignKey(audio => audio.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(audio => audio.Language)
            .WithMany()
            .HasForeignKey(audio => audio.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(audio => new { audio.LocationId, audio.LanguageId });
        builder.HasIndex(audio => audio.TranslationId);
    }
}
