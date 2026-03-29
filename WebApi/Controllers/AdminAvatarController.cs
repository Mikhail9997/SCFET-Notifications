using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Application.DTOs;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RussianTransliteration;

namespace WebApi.Controllers;

[ApiController]
[Route("api/admin/avatars")]
[Authorize(Roles = "Administrator")]
public class AdminAvatarController:ControllerBase
{
    private readonly IAvatarPresetRepository _avatarPresetRepository;
    private readonly string _avatarsPath;

    public AdminAvatarController(IWebHostEnvironment environment, 
        IAvatarPresetRepository avatarPresetRepository)
    {
        _avatarPresetRepository = avatarPresetRepository;
        _avatarsPath = Path.Combine(environment.ContentRootPath, "avatars", "presets");
        
        // Создаем папку если нет
        if (!Directory.Exists(_avatarsPath))
            Directory.CreateDirectory(_avatarsPath);
    }
    
        // Загрузка новой аватарки
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAvatarPreset([FromForm] CreateAvatarPresetDto dto)
    {
        try
        {
            // Проверяем файл
            if (dto.Image == null || dto.Image.Length == 0)
                return BadRequest("Файл не выбран");
            
            // Проверяем расширение
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(dto.Image.FileName).ToLower();
            
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Неподдерживаемый формат изображения");
            
            // Генерируем уникальный ключ
            var presetKey = await GeneratePresetKey(dto.Name);
            
            // Сохраняем файл
            var fileName = $"{presetKey}{extension}";
            var filePath = Path.Combine(_avatarsPath, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.Image.CopyToAsync(stream);
            }
            
            // Создаем запись в БД
            var preset = new AvatarPreset
            {
                PresetKey = presetKey,
                Name = dto.Name,
                FileName = fileName,
                Category = dto.Category
            };
            
            await _avatarPresetRepository.AddAsync(preset);
            
            return Ok(new
            {
                success = true,
                message = "Аватарка успешно загружена",
                preset = new
                {
                    preset.PresetKey,
                    preset.Name,
                    preset.Category,
                    Url = $"/avatars/presets/{fileName}"
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
    
    // Массовая загрузка (zip архивом)
    [HttpPost("upload-batch")]
    public async Task<IActionResult> UploadBatch(IFormFile zipFile)
    {
        var uploaded = new List<string>();
        var errors = new List<string>();
        
        using (var stream = new MemoryStream())
        {
            await zipFile.CopyToAsync(stream);
            using (var archive = new ZipArchive(stream))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".png") || entry.Name.EndsWith(".jpg"))
                    {
                        try
                        {
                            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entry.Name);
                            var presetKey = await GeneratePresetKey(fileNameWithoutExtension);
                            var fileName = entry.Name;
                            var filePath = Path.Combine(_avatarsPath, fileName);
                            
                            // Извлекаем файл
                            entry.ExtractToFile(filePath, true);
                            
                            // Создаем запись
                            var preset = new AvatarPreset
                            {
                                PresetKey = presetKey,
                                Name = presetKey.Replace("_", " "),
                                FileName = fileName,
                                Category = "New",
                            };

                            await _avatarPresetRepository.AddAsync(preset);
                            uploaded.Add(presetKey);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{entry.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }
        
        return Ok(new
        {
            success = true,
            uploaded = uploaded,
            errors = errors
        });
    }
    
    // Удаление аватарки
    [HttpDelete("{presetKey}")]
    public async Task<IActionResult> DeleteAvatarPreset(string presetKey)
    {
        try
        {
            var preset = await _avatarPresetRepository.GetByKey(presetKey);
        
            if (preset == null)
                return NotFound();
        
            // Удаляем файл
            var filePath = Path.Combine(_avatarsPath, preset.FileName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        
            // удаление из БД
            await _avatarPresetRepository.DeleteAsync(preset);
        
            return Ok(new { success = true, message = "Аватарка удалена" });
        }
        catch(Exception ex)
        {
            return BadRequest(new { success = false, message = "Внутренняя ошибка сервера" });
        }
    }
    
    // Удаление всех аватарок
    [HttpDelete("deleteAll")]
    public async Task<IActionResult> DeleteAllAvatarsPreset()
    {
        try
        {
            var presets = await _avatarPresetRepository.GetAllAsync();

            foreach (var preset in presets)
            {
                // Удаляем файл
                var filePath = Path.Combine(_avatarsPath, preset.FileName);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                // удаление из БД
                await _avatarPresetRepository.DeleteAsync(preset);
            }

            return Ok(new { success = true, message = "Аватарки удалены" });
        }
        catch(Exception ex)
        {
            return BadRequest(new { success = false, message = "Внутренняя ошибка сервера" });
        }
    }
    
    private async Task<string> GeneratePresetKey(string name)
    {
        // Транслитерация 
        var latinName = RussianTransliterator.GetTransliteration(name);
    
        // Приводим к нижнему регистру и заменяем пробелы на подчеркивания
        var key = latinName.ToLower()
            .Replace(" ", "_")
            .Replace("__", "_")
            .Trim('_');
    
        // Убираем все символы, кроме латиницы, цифр и подчеркиваний
        key = Regex.Replace(key, @"[^a-z0-9_]", "");
    
        // Если ключ уже существует, добавляем суффикс
        if (await _avatarPresetRepository.Exist(key))
            key = $"{key}_{DateTime.Now.Ticks}";
    
        return key;
    }
}