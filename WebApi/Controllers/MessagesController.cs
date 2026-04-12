using System;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Services;
using Core.Dtos;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebApi.Controllers;

[ApiController]
[Route("api/channels/{channelId}/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IChannelMessageService _messageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IChannelMessageService messageService,
        ICurrentUserService currentUserService,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _currentUserService = currentUserService;
        _logger = logger;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid channelId, [FromQuery] MessageFilterDto filter)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            PagedResult<ChannelMessageDto> messages = await _messageService
                .GetChannelMessagesAsync(channelId, userId.Value, filter);
            
            return Ok(new
            {
                success = true,
                data = messages.Items,
                pagination = new
                {
                    messages.TotalCount,
                    messages.Page,
                    messages.PageSize,
                    messages.TotalPages
                },
                message = "Успех"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get messages for channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при получении сообщений" });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> SendMessage(Guid channelId, [FromForm] SendMessageRequestDto request)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            var dto = new SendMessageDto
            {
                Content = request.Content,
                ReplyToMessageId = request.ReplyToMessageId,
                Image = request.Image
            };
            
            ChannelMessageDto message = await _messageService.SendMessageAsync(channelId, userId.Value, dto);
            
            return Ok(new { success = true, data = message, message = "Сообщение отправлено" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to send message to channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid image for channel {ChannelId}", channelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при отправке сообщения" });
        }
    }
    
    [HttpPut("{messageId}")]
    public async Task<IActionResult> UpdateMessage(Guid channelId, Guid messageId, [FromBody] UpdateMessageDto dto)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            ChannelMessageDto message = await _messageService.UpdateMessageAsync(messageId, userId.Value, dto);
            
            return Ok(new { success = true, data = message, message = "Сообщение обновлено" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update message {MessageId}", messageId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating message {MessageId}", messageId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при обновлении сообщения" });
        }
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid channelId, Guid messageId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            await _messageService.DeleteMessageAsync(messageId, userId.Value);
            
            return Ok(new { success = true, message = "Сообщение удалено" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete message {MessageId}", messageId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка при удалении сообщения" });
        }
    }
    
    [HttpPost("{messageId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid channelId, Guid messageId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            await _messageService.MarkAsReadAsync(messageId, userId.Value);
            
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка" });
        }
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(Guid channelId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            await _messageService.MarkAllAsReadAsync(channelId, userId.Value);
            
            return Ok(new { success = true, message = "Все сообщения отмечены как прочитанные" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all messages as read for channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка" });
        }
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(Guid channelId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
                return Unauthorized(new { success = false, message = "Пользователь не аутентифицирован" });

            int count = await _messageService.GetUnreadCountAsync(channelId, userId.Value);
            
            return Ok(new { success = true, unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for channel {ChannelId}", channelId);
            return StatusCode(500, new { success = false, message = "Произошла ошибка" });
        }
    }
}