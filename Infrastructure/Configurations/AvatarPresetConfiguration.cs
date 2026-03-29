using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class AvatarPresetConfiguration:IEntityTypeConfiguration<AvatarPreset>
{
    public void Configure(EntityTypeBuilder<AvatarPreset> builder)
    {
        // Первичный ключ
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .UseIdentityColumn();
        
        builder.Property(x => x.PresetKey)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);
        
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);
        
        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255)
            .HasDefaultValue(string.Empty);
        
        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);
        
        // Создание индексов
        builder.HasIndex(x => x.PresetKey)
            .IsUnique()
            .HasDatabaseName("ix_avatar_presets_preset_key");
        
        builder.HasIndex(x => x.Category)
            .HasDatabaseName("ix_avatar_presets_category");
        
        builder.HasIndex(x => x.Name)
            .HasDatabaseName("ix_avatar_presets_name");
    }
}