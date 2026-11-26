using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class VisitorSessionConfiguration : IEntityTypeConfiguration<VisitorSession>
{
    public void Configure(EntityTypeBuilder<VisitorSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.StartedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(session => session.LastSeenAt).HasColumnType("datetimeoffset(7)");
        builder.Property(session => session.ExpiresAt).HasColumnType("datetimeoffset(7)");
        builder.HasOne(session => session.Language)
            .WithMany()
            .HasForeignKey(session => session.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => session.DeviceId).IsUnique();
        builder.HasIndex(session => session.ExpiresAt);
    }
}
