using Microsoft.AspNetCore.Http;

namespace Application.DTOs;

public class CreateAvatarPresetDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public IFormFile? Image { get; set; }
}