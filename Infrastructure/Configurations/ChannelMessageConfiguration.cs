using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class ChannelMessageConfiguration : IEntityTypeConfiguration<ChannelMessage>
{
    public void Configure(EntityTypeBuilder<ChannelMessage> builder)
    {
        builder.HasKey(m => m.Id);
        
        builder.Property(m => m.Content)
            .HasMaxLength(2000);
        
        builder.Property(m => m.ImageUrl)
            .HasMaxLength(500);
        
        builder.Property(m => m.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(m => m.UpdatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.HasOne(m => m.Channel)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(m => m.Sender)
            .WithMany(u => u.ChannelMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(m => m.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        
        builder.HasIndex(m => m.ChannelId);
        builder.HasIndex(m => m.SenderId);
        builder.HasIndex(m => m.CreatedAt);
        builder.HasIndex(m => new { m.ChannelId, m.CreatedAt });
        builder.HasIndex(m => new { m.ChannelId, m.IsRead });
        builder.HasIndex(m => new { m.ChannelId, m.SenderId, m.IsRead });
        builder.HasIndex(m => new { m.ReplyToMessageId});
    }
}