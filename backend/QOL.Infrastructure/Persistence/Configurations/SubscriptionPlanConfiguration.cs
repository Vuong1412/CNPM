using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Name).HasMaxLength(120).IsRequired();
        builder.Property(plan => plan.Description).HasMaxLength(1000).IsRequired();
        builder.Property(plan => plan.Price).HasPrecision(18, 2);
        builder.Property(plan => plan.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(plan => plan.IsActive);
    }
}
