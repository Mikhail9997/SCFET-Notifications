using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Core.Models;
using Channel = Core.Models.Channel;

namespace Infrastructure.Configurations;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(c => c.Description)
            .HasMaxLength(500);
            
        builder.HasOne(c => c.Owner)
            .WithMany(u => u.OwnedChannels)
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Property(c => c.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(c => c.UpdatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.OwnerId);
    }
}