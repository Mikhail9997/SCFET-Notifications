using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class UserFavoriteNotificationConfiguration: IEntityTypeConfiguration<UserFavoriteNotification>
{
    public void Configure(EntityTypeBuilder<UserFavoriteNotification> builder)
    {
        builder.HasKey(x => new { x.UserId, x.NotificationId });
        
        builder.Property(f => f.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.HasOne(x => x.User)
            .WithMany(u => u.FavoriteNotifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(x => x.Notification)
            .WithMany(n => n.FavoriteByUsers)
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_UserFavoriteNotifications_UserId");
            
        builder.HasIndex(x => x.NotificationId)
            .HasDatabaseName("IX_UserFavoriteNotifications_NotificationId");
        
        builder.HasIndex(x => new { x.UserId, x.NotificationId })
            .HasDatabaseName("IX_UserFavoriteNotifications_UserId_NotificationId");
    }
}