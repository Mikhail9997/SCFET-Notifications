using Application.Common.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController:ControllerBase
{
    private readonly FileService _fileService;
    private readonly ProfileService _profileService;
    private readonly ICurrentUserService _currentUserService;
    private readonly string _uploadsFolder;

    public ProfileController(FileService fileService, ProfileService profileService, 
        IWebHostEnvironment environment, ICurrentUserService currentUserService)
    {
        _fileService = fileService;
        _profileService = profileService;
        _currentUserService = currentUserService;

        // Определяем папку для загрузок относительно корня приложения
        _uploadsFolder = Path.Combine(environment.ContentRootPath, "uploads", "avatars");
        
        // Создаем папку если не существует
        if (!Directory.Exists(_uploadsFolder))
        {
            Directory.CreateDirectory(_uploadsFolder);
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        Guid currentUserId = _currentUserService.UserId.Value;
        try
        {
            string avatarUrl = await _fileService.SaveImageAsync(avatar, _uploadsFolder);
            await _profileService.UploadAvatar(currentUserId, avatarUrl);
            return Ok(new { message = "Внутренняя ошибка сервера", success=true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }
    
    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RemoveAvatar()
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        Guid currentUserId = _currentUserService.UserId.Value;
        try
        {
            await _profileService.RemoveAvatar(currentUserId, _uploadsFolder);
            return Ok(new { message = "Внутренняя ошибка сервера", success=true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }
}