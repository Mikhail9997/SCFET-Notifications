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
    private readonly IAvatarService _avatarService;
    private readonly ICurrentUserService _currentUserService;

    public ProfileController(
        ICurrentUserService currentUserService, 
        IAvatarService avatarService)
    {
        _currentUserService = currentUserService;
        _avatarService = avatarService;
    }

    [HttpPut("uploadAvatar")]
    public async Task<IActionResult> UploadAvatar([FromBody] UploadAvatarDto dto)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        Guid currentUserId = _currentUserService.UserId.Value;
        try
        {
            await _avatarService.UpdateAvatarPresetAsync(currentUserId, dto.AvatarPresetKey);
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
    
    [Authorize(Roles = "Teacher,Administrator")]
    [HttpPut("uploadCustomAvatar")]
    public async Task<IActionResult> UploadCustomAvatar([FromForm]CreateCustomPresetDto dto)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        Guid currentUserId = _currentUserService.UserId.Value;
        try
        {
            await _avatarService.UploadCustomPresetAsync(currentUserId, dto);
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