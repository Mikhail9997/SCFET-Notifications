using Application.Common.Interfaces;
using Application.DTOs;
using Application.Services;
using Core.Dtos;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelsController: ControllerBase
{
    private readonly IChannelService _channelService;
    private readonly ILogger<ChannelsController> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IChannelUserService _channelUserService;
    private readonly IChannelInvitationRepository _invitationRepository;

    public ChannelsController(
        IChannelService channelService,
        ILogger<ChannelsController> logger, 
        ICurrentUserService currentUserService, 
        IChannelUserService channelUserService, 
        IChannelInvitationRepository invitationRepository)
    {
        _channelService = channelService;
        _logger = logger;
        _currentUserService = currentUserService;
        _channelUserService = channelUserService;
        _invitationRepository = invitationRepository;
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateChannel([FromBody] CreateChannelDto dto)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            var channel = await _channelService.CreateChannelAsync(userId.Value, dto);
            
            return Ok(new 
            { 
                success = true, 
                data = channel, 
                message = "Канал успешно создан" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create channel");
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating channel");
            return StatusCode(500, new { success = false, message = "Произошла ошибка при создании канала" });
        }
    }
    
    [HttpGet("my-channels")]
    public async Task<IActionResult> GetMyChannels([FromQuery] ChannelFilterEntity filter)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            PagedResult<ChannelDto> pagedChannels = await _channelService
                .GetUserChannelsPaginatedAsync(userId.Value, filter);
            
            return Ok(new 
            { 
                success = true, 
                data = pagedChannels.Items,
                pagination = new
                {
                    pagedChannels.TotalCount,
                    pagedChannels.Page,
                    pagedChannels.PageSize,
                    pagedChannels.TotalPages
                },
                message = "Успех" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user channels");
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении каналов" });
        }
    }

    [HttpGet("all")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetAllChannels([FromQuery] ChannelFilterEntity filter)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            PagedResult<ChannelDto> pagedChannels = await _channelService
                .GetAllChannelsPaginatedAsync(filter, userId.Value);
            
            return Ok(new 
            { 
                success = true, 
                data = pagedChannels.Items,
                pagination = new
                {
                    pagedChannels.TotalCount,
                    pagedChannels.Page,
                    pagedChannels.PageSize,
                    pagedChannels.TotalPages
                },
                message = "Успех" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all channels");
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении каналов" });
        }
    }
    
    [HttpGet("{channelId}")]
    public async Task<IActionResult> GetChannelById(Guid channelId)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            ChannelDto? channel = await _channelService
                .GetChannelByIdAsync(channelId, userId.Value);
            
            if (channel == null)
            {
                return NotFound(new { success = false, message = "Канал не найден" });
            }
            
            // Устанавливаем флаги для текущего пользователя
            var membership = await _channelService.GetChannelMemberAsync(channelId, userId.Value);
            
            if (membership != null)
            {
                channel.IsMember = true;
                channel.UserRole = membership.ChannelRole;
                channel.IsOwner = membership.ChannelRole == ChannelRole.Owner;
            }
            
            return Ok(new { success = true, data = channel, message = "Успех"});
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get channel by id {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel by id {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении канала" });
        }
    }
    
    [HttpGet("invitations")]
    public async Task<IActionResult> GetMyInvitations([FromQuery] ChannelFilterEntity filter)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            PagedResult<ChannelInvitationDto> pagedInvitations = await _channelService
                .GetUserInvitationsPaginatedAsync(userId.Value, filter);
            
            return Ok(new 
            { 
                success = true, 
                data = pagedInvitations.Items,
                pagination = new
                {
                    pagedInvitations.TotalCount,
                    pagedInvitations.Page,
                    pagedInvitations.PageSize,
                    pagedInvitations.TotalPages
                },
                message = "Успех" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user invitations");
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении приглашений" });
        }
    }
    
    [HttpGet("sent-invitations")]
    public async Task<IActionResult> GetSentInvitations([FromQuery] ChannelFilterEntity filter)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            PagedResult<ChannelInvitationDto> pagedInvitations = await _channelService
                .GetUserSentInvitationsPaginatedAsync(userId.Value, filter);
            
            return Ok(new 
            { 
                success = true, 
                data = pagedInvitations.Items,
                pagination = new
                {
                    pagedInvitations.TotalCount,
                    pagedInvitations.Page,
                    pagedInvitations.PageSize,
                    pagedInvitations.TotalPages
                },
                message = "Успех" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sent invitations");
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении отправленных приглашений" });
        }
    }
    
    [HttpPost("{channelId}/invite")]
    public async Task<IActionResult> InviteUsers(Guid channelId, [FromBody] InviteUsersDto dto)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            await _channelService.InviteUsersToChannelAsync(channelId, userId.Value, dto.UserIds, dto.Message);
            
            return Ok(new 
            { 
                success = true, 
                message = "Приглашения успешно отправлены" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to invite users to channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting users to channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при отправке приглашений" });
        }
    }
    
    [HttpGet("{channelId}/members")]
    public async Task<IActionResult> GetChannelMembers(Guid channelId)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            List<ChannelMemberDto> members = await _channelService.GetChannelMembersAsync(channelId);
            return Ok(new { success = true, data = members, message = "Успех"  });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel members for channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении участников канала" });
        }
    }

    [HttpPut("{channelId}/members/{userId}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid channelId, Guid userId, [FromBody] UpdateRoleRequest request)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            await _channelService.UpdateUserRoleAsync(channelId, userId, request.NewRole, currentUserId.Value);
            
            return Ok(new { success = true, message = "Роль участника успешно обновлена" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update member role in channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member role in channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при обновлении роли участника" });
        }
    }

    [HttpDelete("{channelId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid channelId, Guid userId)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            await _channelService.RemoveUserFromChannelAsync(channelId, userId, currentUserId.Value);
            
            return Ok(new { success = true, message = "Участник удален из канала" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to remove member from channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при удалении участника" });
        }
    }
    
    [HttpPost("{channelId}/leave")]
    public async Task<IActionResult> LeaveChannel(Guid channelId)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            await _channelService.LeaveChannelAsync(channelId, currentUserId.Value);
            
            return Ok(new { success = true, message = "Вы покинули канал" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to leave channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при выходе из канала" });
        }
    }
    
    [HttpGet("{channelId}/available-users")]
    public async Task<IActionResult> GetAvailableUsers(Guid channelId, [FromQuery] AvailableUsersFilterDto filter)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            PagedResult<AvailableUserDto> pagedUsers = await _channelUserService
                .GetAvailableUsersForChannelAsync(channelId, currentUserId.Value, filter);
            
            return Ok(new 
            { 
                success = true, 
                data = pagedUsers.Items,
                pagination = new
                {
                    pagedUsers.TotalCount,
                    pagedUsers.Page,
                    pagedUsers.PageSize,
                    pagedUsers.TotalPages
                },
                message = "Успех" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get available users for channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available users for channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении списка пользователей" });
        }
    }
    
    [HttpPost("invitations/{invitationId}/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid invitationId)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            await _channelService.AcceptInvitationAsync(invitationId, currentUserId.Value);
        
            return Ok(new 
            { 
                success = true, 
                message = "Вы успешно присоединились к каналу" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to accept invitation {InvitationId}", invitationId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation {InvitationId}", invitationId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при принятии приглашения" });
        }
    }
    
    [HttpPost("invitations/{invitationId}/decline")]
    public async Task<IActionResult> DeclineInvitation(Guid invitationId)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        try
        {
            await _channelService.DeclineInvitationAsync(invitationId, currentUserId.Value);
            
            return Ok(new 
            { 
                success = true, 
                message = "Приглашение отклонено" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to decline invitation {InvitationId}", invitationId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining invitation {InvitationId}", invitationId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при отклонении приглашения" });
        }
    }
    
    [HttpDelete("invitations/{invitationId}/cancel")]
    public async Task<IActionResult> CancelInvitation(Guid invitationId)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        
        try
        {
            await _channelService.CancelInvitationAsync(invitationId, currentUserId.Value);
        
            return Ok(new 
            { 
                success = true, 
                message = "Приглашение отменено" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to cancel invitation {InvitationId}", invitationId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling invitation {InvitationId}", invitationId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при отмене приглашения" });
        }
    }
    
    [HttpDelete("invitations/{invitationId}")]
    public async Task<IActionResult> DeleteInvitation(Guid invitationId)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }
        try
        {
            // Проверяем, что приглашение принадлежит пользователю
            var invitation = await _invitationRepository.GetByIdAsync(invitationId);
            
            if (invitation == null)
            {
                return NotFound(new { success = false, message = "Приглашение не найдено" });
            }
            
            if (invitation.InviteeId != currentUserId.Value)
            {
                return BadRequest(new { success = false, message = "Это приглашение предназначено не вам" });
            }
            
            // Если приглашение в статусе Pending, отклоняем его
            if (invitation.Status == InvitationStatus.Pending)
            {
                await _channelService.DeclineInvitationAsync(invitationId, currentUserId.Value);
            }
            else
            {
                // Если уже обработано, можно просто удалить
                await _invitationRepository.DeleteAsync(invitation);
            }
            
            return Ok(new 
            { 
                success = true, 
                message = "Приглашение удалено" 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete invitation {InvitationId}", invitationId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting invitation {InvitationId}", invitationId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при удалении приглашения" });
        }
    }
    
    private Guid? GetCurrentUserId()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return null;
        }

        return _currentUserService.UserId.Value;
    }
}

public class UpdateRoleRequest
{
    public ChannelRole NewRole { get; set; }
}