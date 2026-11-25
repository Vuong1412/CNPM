using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionOrderConfiguration : IEntityTypeConfiguration<SubscriptionOrder>
{
    public void Configure(EntityTypeBuilder<SubscriptionOrder> builder)
    {
        builder.HasKey(order => order.Id);
        builder.Property(order => order.PlanName).HasMaxLength(120).IsRequired();
        builder.Property(order => order.Amount).HasPrecision(18, 2);
        builder.Property(order => order.Currency).HasMaxLength(3).IsRequired();
        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(order => order.ReviewNote).HasMaxLength(1000);
        builder.HasOne(order => order.User)
            .WithMany()
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Plan)
            .WithMany()
            .HasForeignKey(order => order.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(order => new { order.UserId, order.CreatedAt });
        builder.HasIndex(order => order.Status);
    }
}
