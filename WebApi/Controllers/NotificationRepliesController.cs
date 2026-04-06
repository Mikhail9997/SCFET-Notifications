using Application.Common.Interfaces;
using Application.DTOs;
using Application.Services;
using AutoMapper;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;


[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationRepliesController:ControllerBase
{
    private readonly NotificationReplyService _notificationReplyService;
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationReplyRepository _notificationReplyRepository;
    private readonly IMapper _mapper;
    private readonly IAvatarService _avatarService;

    public NotificationRepliesController(NotificationReplyService notificationReplyService,
        ICurrentUserService currentUserService, INotificationReplyRepository notificationReplyRepository, 
        IMapper mapper, IAvatarService avatarService, 
        INotificationRepository notificationRepository)
    {
        _notificationReplyService = notificationReplyService;
        _currentUserService = currentUserService;
        _notificationReplyRepository = notificationReplyRepository;
        _mapper = mapper;
        _avatarService = avatarService;
        _notificationRepository = notificationRepository;
    }

    [HttpGet("notification/{notificationId}/replies")]
    public async Task<IActionResult> GetNotificationReplies(Guid notificationId, [FromQuery] FilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        
        var userId = _currentUserService.UserId.Value;
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                return BadRequest(new {message = "Уведомление не найдено", success = false});
            }
            if (notification.Receivers.All(r => r.UserId != userId))
            {
                return BadRequest(new {message = "Вы не являетесь получателем данного уведомления", success = false});
            }
            
            var filter = _mapper.Map<FilterEntity>(query);
            var pagedResult = await _notificationReplyRepository
                .GetNotificationsReplyByNotificationId(notificationId, filter);

            var items = new List<ReplyDto>();

            foreach (NotificationReply item in pagedResult.Items)
            {
                var replyDto = _mapper.Map<ReplyDto>(item);
                replyDto.UserAvatarUrl = await _avatarService.GetAvatarUrl(item.User.AvatarPresetKey);
                
                items.Add(replyDto);
            }
            
            GetItemsDto<ReplyDto> result = new GetItemsDto<ReplyDto>()
            {
                Items = items,
                TotalCount = pagedResult.TotalCount,
                Page = pagedResult.Page,
                PageSize = pagedResult.PageSize
            };

            return Ok(new {message = "Успешно", data = result, success = true});
        }
        catch(Exception ex)
        {
            return BadRequest(new {message = "Произошла неизвестная ошибка", success = false});
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateReply(CreateReplyDto dto)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var userId = _currentUserService.UserId.Value;
        try
        {
            await _notificationReplyService.SendNotificationReplyAsync(dto, userId);
            return Ok(new {message = "Успешно отправлено", success = true});
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message, success = false});
        }
        catch(Exception ex)
        {
            return BadRequest(new {message = "Произошла неизвестная ошибка", success = false});
        }
    }

    [HttpPut("{id}/update")]
    public async Task<IActionResult> UpdateReply(Guid id, UpdateNotificationReplyDto dto)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        
        try
        {
            await _notificationReplyService.UpdateNotificationReplyAsync(id, dto);
            return Ok(new {message = "Успешно обновлено", success = true});
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message, success = false});
        }
        catch(Exception ex)
        {
            return BadRequest(new {message = "Произошла неизвестная ошибка", success = false});
        }
    }
    
    [HttpDelete("{id}/remove")]
    public async Task<IActionResult> RemoveNotificationReply(Guid id )
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        try
        {
            await _notificationReplyService.RemoveNotificationReplyAsync(id);
            return Ok(new {message = "Успешно удалено", success = true});
        }
        catch(InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message, success = false});
        }
        catch(Exception ex)
        {
            return BadRequest(new {message = "Произошла неизвестная ошибка", success = false});
        }
    }
}