using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class ChannelUserConfiguration : IEntityTypeConfiguration<ChannelUser>
{
    public void Configure(EntityTypeBuilder<ChannelUser> builder)
    {
        builder.HasKey(cu => cu.Id);
        
        builder.Property(c => c.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(c => c.UpdatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.HasOne(cu => cu.Channel)
            .WithMany(c => c.ChannelUsers)
            .HasForeignKey(cu => cu.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(cu => cu.User)
            .WithMany(u => u.ChannelMemberships)
            .HasForeignKey(cu => cu.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Уникальный индекс для предотвращения дублирования
        builder.HasIndex(cu => new { cu.ChannelId, cu.UserId })
            .IsUnique();
    }
}