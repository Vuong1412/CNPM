using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class ListeningHistoryConfiguration : IEntityTypeConfiguration<ListeningHistory>
{
    public void Configure(EntityTypeBuilder<ListeningHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.ListenedAt)
            .HasColumnType("datetimeoffset(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(history => history.User)
            .WithMany(user => user.ListeningHistory)
            .HasForeignKey(history => history.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(history => history.Location)
            .WithMany(location => location.ListeningHistory)
            .HasForeignKey(history => history.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(history => history.AudioFile)
            .WithMany()
            .HasForeignKey(history => history.AudioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(history => history.Language)
            .WithMany()
            .HasForeignKey(history => history.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(history => new { history.UserId, history.ListenedAt });
        builder.HasIndex(history => history.LocationId);
        builder.HasIndex(history => history.LanguageId);
    }
}
