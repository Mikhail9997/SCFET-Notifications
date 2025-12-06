using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class NotificationReceiverConfiguration : IEntityTypeConfiguration<NotificationReceiver>
{
    public void Configure(EntityTypeBuilder<NotificationReceiver> builder)
    {
        builder.HasKey(r => r.Id);
            
        builder.Property(r => r.IsRead)
            .IsRequired();
        
        builder.Property(n => n.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(n => n.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        // Составной ключ для предотвращения дублирования
        builder.HasIndex(r => new { r.NotificationId, r.UserId })
            .IsUnique();

        // Отношения
        builder.HasOne(r => r.User)
            .WithMany(u => u.ReceivedNotifications)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}