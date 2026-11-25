using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.Configurations;

internal sealed class LocationReviewConfiguration : IEntityTypeConfiguration<LocationReview>
{
    public void Configure(EntityTypeBuilder<LocationReview> builder)
    {
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Comment).HasMaxLength(2000);
        builder.HasOne(review => review.User)
            .WithMany()
            .HasForeignKey(review => review.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(review => review.Location)
            .WithMany()
            .HasForeignKey(review => review.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(review => new { review.UserId, review.LocationId }).IsUnique();
        builder.HasIndex(review => new { review.LocationId, review.IsVisible, review.CreatedAt });
    }
}
