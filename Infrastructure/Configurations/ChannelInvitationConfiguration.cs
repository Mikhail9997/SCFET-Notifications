using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class ChannelInvitationConfiguration : IEntityTypeConfiguration<ChannelInvitation>
{
    public void Configure(EntityTypeBuilder<ChannelInvitation> builder)
    {
        builder.HasKey(ci => ci.Id);
        
        builder.Property(c => c.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(c => c.UpdatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.HasOne(ci => ci.Channel)
            .WithMany(c => c.Invitations)
            .HasForeignKey(ci => ci.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(ci => ci.Inviter)
            .WithMany(u => u.SentInvitations)
            .HasForeignKey(ci => ci.InviterId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(ci => ci.Invitee)
            .WithMany(u => u.ReceivedInvitations)
            .HasForeignKey(ci => ci.InviteeId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasIndex(ci => ci.Status);
        builder.HasIndex(ci => new { ci.ChannelId, ci.InviteeId });
    }
}