using Application.DTOs;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Application.Services;

public interface IAvatarService
{
     Task<string> GetAvatarUrl(string? avatarPresetKey);
    Task<List<AvatarPresetDto>> GetAllPresetsAsync();
}

public class AvatarService:IAvatarService
{
    private readonly IConfiguration _configuration;
    private readonly IAvatarPresetRepository _presetRepository;

    public AvatarService(IAvatarPresetRepository presetRepository, IConfiguration configuration)
    {
        _presetRepository = presetRepository;
        _configuration = configuration;
    }

    public async Task<string> GetAvatarUrl(string? avatarPresetKey)
    {
        if (string.IsNullOrEmpty(avatarPresetKey))
        {
            return $"{_configuration["BaseUrl"]}/avatars/default.png";
        }
        // Получаем расширение из БД
        var preset = await _presetRepository.GetByKey(avatarPresetKey);
        var extension = Path.GetExtension(preset?.FileName) ?? ".png";
        // Формируем URL к статическому файлу
        return $"{_configuration["BaseUrl"]}/avatars/presets/{avatarPresetKey}{extension.ToLowerInvariant()}";
    }

    public async Task<List<AvatarPresetDto>> GetAllPresetsAsync()
    {
        var presets = await _presetRepository.GetAllAsync();
        List<AvatarPresetDto> result = new List<AvatarPresetDto>();

        foreach (var p in presets)
        {
            string extension = Path.GetExtension(p.FileName);
            var dto = new AvatarPresetDto
            {
                Key = p.PresetKey,
                Name = p.Name,
                Category = p.Category,
                ImageUrl = $"{_configuration["BaseUrl"]}/avatars/presets/{p.PresetKey}{extension}"
            };
            result.Add(dto);
        }
        return result;
    }
}