using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.PlanName).HasMaxLength(120).IsRequired();
        builder.Property(subscription => subscription.Currency).HasMaxLength(3).IsRequired();
        builder.Property(subscription => subscription.Amount).HasPrecision(18, 2);
        builder.Property(subscription => subscription.StartsAt).HasColumnType("datetimeoffset(7)");
        builder.Property(subscription => subscription.EndsAt).HasColumnType("datetimeoffset(7)");
        builder.HasOne(subscription => subscription.User)
            .WithMany()
            .HasForeignKey(subscription => subscription.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(subscription => subscription.Plan)
            .WithMany()
            .HasForeignKey(subscription => subscription.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(subscription => subscription.Order)
            .WithMany()
            .HasForeignKey(subscription => subscription.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(subscription => new { subscription.UserId, subscription.IsActive, subscription.EndsAt });
        builder.HasIndex(subscription => subscription.OrderId).IsUnique();
    }
}
