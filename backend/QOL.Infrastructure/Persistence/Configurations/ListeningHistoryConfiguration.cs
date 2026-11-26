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
        builder.Property(history => history.TriggerMethod)
            .HasConversion(
                method => ToDatabaseMethod(method),
                method => FromDatabaseMethod(method))
            .HasMaxLength(10)
            .HasDefaultValueSql("N'TAP'");
        builder.HasOne(history => history.User)
            .WithMany(user => user.ListeningHistory)
            .HasForeignKey(history => history.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(history => history.Session)
            .WithMany(session => session.ListeningHistory)
            .HasForeignKey(history => history.SessionId)
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
        builder.HasIndex(history => history.SessionId);
        builder.HasIndex(history => history.LocationId);
        builder.HasIndex(history => history.LanguageId);
    }

    private static string ToDatabaseMethod(ListeningTriggerMethod method) =>
        method switch
        {
            ListeningTriggerMethod.Tap => "TAP",
            ListeningTriggerMethod.AutoGps => "AUTO_GPS",
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

    private static ListeningTriggerMethod FromDatabaseMethod(string method) =>
        method switch
        {
            "TAP" => ListeningTriggerMethod.Tap,
            "AUTO_GPS" => ListeningTriggerMethod.AutoGps,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
}
