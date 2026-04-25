using Application.DTOs;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Application.Services;

public interface IAvatarService
{
     Task UpdateAvatarPresetAsync(Guid userId, string presetKey);
     Task<string> GetAvatarUrl(string? avatarPresetKey);
     Task<Dictionary<string, string>> GetAvatarUrlsAsync(IEnumerable<string> presetKeys);
     Task<List<AvatarPresetDto>> GetAllPresetsAsync();
     Task UploadCustomPresetAsync(Guid userId, CreateCustomPresetDto dto);
     Task RemoveCustomAvatarAsync(string presetKey);
}

public class AvatarService:IAvatarService
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly IAvatarPresetRepository _presetRepository;
    private readonly string _customAvatarsPath;

    public AvatarService(IAvatarPresetRepository presetRepository, 
        IConfiguration configuration, 
        IUserRepository userRepository,
        IWebHostEnvironment environment)
    {
        _presetRepository = presetRepository;
        _configuration = configuration;
        _userRepository = userRepository;

        _customAvatarsPath = Path.Combine(environment.ContentRootPath, "uploads", "avatars", "presets", "custom");
        
        // Создаем папку если нет
        if (!Directory.Exists(_customAvatarsPath))
            Directory.CreateDirectory(_customAvatarsPath);
    }

    public async Task UpdateAvatarPresetAsync(Guid userId, string presetKey)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Пользователь не найден");
        
        user.AvatarPresetKey = presetKey;
        await _userRepository.UpdateAsync(user);
        
        // удаляем кастомную аватарку если она есть
        // Id пользователя является presetKey
        
        await RemoveCustomAvatarAsync(user.Id.ToString());
    }
    
    public async Task<string> GetAvatarUrl(string? avatarPresetKey)
    {
        if (string.IsNullOrEmpty(avatarPresetKey))
        {
            return $"{_configuration["BaseUrl"]}/uploads/avatars/presets/default/default.jpg";
        }
        // Получаем расширение из БД
        var preset = await _presetRepository.GetByKey(avatarPresetKey);
        var extension = Path.GetExtension(preset?.FileName) ?? ".png";
        // Формируем URL к статическому файлу
        return $"{_configuration["BaseUrl"]}/uploads/avatars/presets/{preset?.Category}/{avatarPresetKey}{extension.ToLowerInvariant()}";
    }

    public async Task<Dictionary<string, string>> GetAvatarUrlsAsync(IEnumerable<string> presetKeys)
    {
        var result = new Dictionary<string, string>();
        var keys = presetKeys.Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
        
        if (!keys.Any())
            return result;

        // Загружаем все пресеты одним запросом
        var presets = await _presetRepository.GetByKeysAsync(keys);
        var presetDict = presets.ToDictionary(p => p.PresetKey, p => p);

        foreach (var key in keys)
        {
            if (presetDict.TryGetValue(key, out var preset))
            {
                var extension = Path.GetExtension(preset.FileName) ?? ".png";
                result[key] = $"{_configuration["BaseUrl"]}/uploads/avatars/presets/{preset.Category}/{key}{extension.ToLowerInvariant()}";
            }
            else
            {
                // Fallback если пресет не найден
                result[key] = $"{_configuration["BaseUrl"]}/uploads/avatars/presets/default/default.jpg";
            }
        }

        return result;
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
                ImageUrl = $"{_configuration["BaseUrl"]}/uploads/avatars/presets/{p.Category}/{p.PresetKey}{extension}"
            };
            result.Add(dto);
        }
        return result;
    }
    
    public async Task UploadCustomPresetAsync(Guid userId, CreateCustomPresetDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Пользователь не найден");

        // Проверяем файл
        if (dto.Image == null || dto.Image.Length == 0)
            throw new InvalidOperationException("Файл не выбран");
            
        // Проверяем расширение
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".jfif" };
        var extension = Path.GetExtension(dto.Image.FileName).ToLower();
            
        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Неподдерживаемый формат изображения");
        
        // уникальный ключ - id пользователя
        var presetKey = user.Id.ToString();
        
        var fileName = $"{presetKey}{extension}";
        string filePath = Path.Combine(_customAvatarsPath, fileName);
        
        // если у пользователя уже есть кастомная аватарка - удаляем
        await RemoveCustomAvatarAsync(presetKey);
        
        // Сохраняем файл
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await dto.Image.CopyToAsync(stream);
        }
        
        // Создаем запись в БД
        var preset = new AvatarPreset
        {
            PresetKey = presetKey,
            Name = $"{user.FirstName} {user.LastName}",
            FileName = fileName,
            Category = "custom"
        };
        
        await _presetRepository.AddAsync(preset);
        
        // применяем созданную аватарку пользователю
        user.AvatarPresetKey = presetKey;
        await _userRepository.UpdateAsync(user);
    }

    public async Task RemoveCustomAvatarAsync(string presetKey)
    {
        var preset = await _presetRepository.GetByKey(presetKey);
        if (preset == null) return;
        string filePath = Path.Combine(_customAvatarsPath, preset.FileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            
        }
        
        await _presetRepository.RemoveByKey(presetKey);
    }
}