using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Group = Core.Models.Group;

namespace Infrastructure.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.HasKey(g => g.Id);
            
        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.HasIndex(g => g.Name)
            .IsUnique();

        builder.Property(g => g.Description)
            .HasMaxLength(500);
        
        builder.Property(g => g.CreatedAt)
            .HasColumnType("timestamp with time zone");
        
        builder.Property(g => g.UpdatedAt)
            .HasColumnType("timestamp with time zone");
    }
}