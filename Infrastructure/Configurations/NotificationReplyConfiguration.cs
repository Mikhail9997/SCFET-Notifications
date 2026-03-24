using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class NotificationReplyConfiguration: IEntityTypeConfiguration<NotificationReply>
{
    public void Configure(EntityTypeBuilder<NotificationReply> builder)
    {
        builder.ToTable("NotificationReplies");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.NotificationId)
            .IsRequired();
            
        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);
            
        builder.Property(n => n.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(n => n.UpdatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.HasIndex(x => x.NotificationId)
            .HasDatabaseName("IX_NotificationReplies_NotificationId");
            
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_NotificationReplies_UserId");
        
        builder.HasIndex(x => new { x.NotificationId, x.CreatedAt })
            .HasDatabaseName("IX_NotificationReplies_NotificationId_CreatedAt");
        
        // Связи 
        
        // Связь с Notification (один ко многим)
        builder.HasOne(x => x.Notification)
            .WithMany(n => n.Replies) 
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade) // При удалении уведомления удаляются все ответы
            .HasConstraintName("FK_NotificationReplies_Notifications_NotificationId");
            
        // Связь с User (один ко многим)
        builder.HasOne(x => x.User)
            .WithMany(u => u.Replies) 
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict) // Не удаляем ответы при удалении пользователя
            .HasConstraintName("FK_NotificationReplies_Users_UserId");
    
    }
}