using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
            
        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<string>();
        
        builder.Property(n => n.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(n => n.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        // Отношения
        builder.HasOne(n => n.Sender)
            .WithMany(u => u.SentNotifications)
            .HasForeignKey(n => n.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(n => n.Receivers)
            .WithOne(r => r.Notification)
            .HasForeignKey(r => r.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(n => n.FavoriteByUsers)
            .WithOne(f => f.Notification)
            .HasForeignKey(f => f.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}