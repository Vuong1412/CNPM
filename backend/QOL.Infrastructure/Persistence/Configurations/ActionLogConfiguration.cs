using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class ActionLogConfiguration : IEntityTypeConfiguration<ActionLog>
{
    public void Configure(EntityTypeBuilder<ActionLog> builder)
    {
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Action).HasMaxLength(64).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(100);
        builder.Property(log => log.Detail).HasMaxLength(1000);
        builder.Property(log => log.OccurredAt).HasColumnType("datetimeoffset(7)");
        builder.HasOne(log => log.Actor)
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(log => new { log.ActorUserId, log.OccurredAt });
    }
}
