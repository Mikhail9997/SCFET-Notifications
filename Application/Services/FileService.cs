using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class FileService
{
    private readonly ILogger<FileService> _logger;

    public FileService(ILogger<FileService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveImageAsync(IFormFile image, string uploadsFolder)
    {
        // Проверяем тип файла
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            throw new ArgumentException("Invalid image format");

        // Проверяем размер файла (макс 15MB)
        if (image.Length > 15 * 1024 * 1024)
            throw new ArgumentException("Image size too large");

        // Создаем уникальное имя файла
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        return $"/uploads/notifications/{fileName}";
    }
    
    public async Task DeleteNotificationImagesAsync(string imageUrl, string uploadsFolder)
    {
       
        try
        {
            if (await DeleteImageFileAsync(imageUrl, uploadsFolder))
            {
                _logger.LogInformation("Deleted image: {ImageUrl}", imageUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete image: {ImageUrl}", imageUrl);
        }
    }
    
    private async Task<bool> DeleteImageFileAsync(string imageUrl, string uploadsFolder)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return false;

        try
        {
            // Извлекаем имя файла из URL
            var fileName = Path.GetFileName(imageUrl);
            if (string.IsNullOrEmpty(fileName))
                return false;

            var filePath = Path.Combine(uploadsFolder, fileName);

            // Проверяем что файл существует и находится в разрешенной директории
            if (File.Exists(filePath) && IsInAllowedDirectory(filePath, uploadsFolder))
            {
                File.Delete(filePath);
                await Task.Delay(100);
                return !File.Exists(filePath);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {ImageUrl}", imageUrl);
            return false;
        }
    }

    public bool IsImagesEquals(string fileName1, string fileName2)
    {
        if (fileName1.Equals(fileName2)) return true;
        return false;
    }
    
    private bool IsInAllowedDirectory(string filePath, string uploadsFolder)
    {
        return filePath.StartsWith(uploadsFolder);
    }
}