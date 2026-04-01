using Microsoft.AspNetCore.Http;

namespace Application.DTOs;

public class CreateCustomPresetDto
{
    public IFormFile? Image { get; set; }
}