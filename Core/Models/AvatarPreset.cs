namespace Core.Models;

public class AvatarPreset
{
    public int Id { get; set; }
    public string PresetKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Category { get; set; }  = string.Empty;
} 