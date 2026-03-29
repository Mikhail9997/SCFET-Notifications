using Application.Common.Interfaces;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController:ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly IAvatarService _avatarService;
    private readonly ICurrentUserService _currentUserService;

    public ProfileController(ProfileService profileService, 
        IWebHostEnvironment environment, ICurrentUserService currentUserService, 
        IAvatarService avatarService)
    {
        _profileService = profileService;
        _currentUserService = currentUserService;
        _avatarService = avatarService;


    }

    [HttpPost("uploadAvatar")]
    public async Task<IActionResult> UploadAvatar(string avatarPresetKey)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        Guid currentUserId = _currentUserService.UserId.Value;
        try
        {
            await _profileService.UpdateAvatarPreset(currentUserId, avatarPresetKey);
            return Ok(new { message = "Успех", success=true });
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

    [HttpGet("avatars")]
    public async Task<IActionResult> GetAllAvatarPresets()
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        
        try
        {
            List<AvatarPresetDto> result = await _avatarService.GetAllPresetsAsync();
            return Ok(new { message = "Успех", data=result ,success=true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }
}