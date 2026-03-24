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
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationReplyRepository _notificationReplyRepository;
    private readonly IMapper _mapper;

    public NotificationRepliesController(NotificationReplyService notificationReplyService,
        ICurrentUserService currentUserService, INotificationReplyRepository notificationReplyRepository, 
        IMapper mapper)
    {
        _notificationReplyService = notificationReplyService;
        _currentUserService = currentUserService;
        _notificationReplyRepository = notificationReplyRepository;
        _mapper = mapper;
    }

    [HttpGet("notification/{notificationId}/replies")]
    public async Task<IActionResult> GetNotificationReplies(Guid notificationId, [FromQuery] NotificationFilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        try
        {
            var filter = _mapper.Map<NotificationFilterEntity>(query);
            var pagedResult = await _notificationReplyRepository
                .GetNotificationsReplyByNotificationId(notificationId, filter);

            GetItemsDto<ReplyDto> result = new GetItemsDto<ReplyDto>()
            {
                Items = _mapper.Map<List<ReplyDto>>(pagedResult.Items),
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
    public async Task<ActionResult<ReplyDto>> CreateReply(CreateReplyDto dto)
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
    public async Task<ActionResult<ReplyDto>> UpdateReply(Guid id, UpdateNotificationReplyDto dto)
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