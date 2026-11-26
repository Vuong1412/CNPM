using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Provider).HasMaxLength(32).IsRequired();
        builder.Property(payment => payment.ProviderTxnId).HasMaxLength(100).IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(payment => payment.CreatedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(payment => payment.PaidAt).HasColumnType("datetimeoffset(7)");
        builder.HasOne(payment => payment.Order)
            .WithMany()
            .HasForeignKey(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(payment => payment.ProviderTxnId).IsUnique();
        builder.HasIndex(payment => new { payment.OrderId, payment.CreatedAt });
    }
}
