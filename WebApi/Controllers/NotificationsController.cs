using Application.Common.Interfaces;
using Application.DTOs;
using Application.Exceptions;
using Application.Extensions;
using Application.Hubs;
using Application.Services;
using Application.Utils;
using AutoMapper;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController:ControllerBase
{

    private readonly FileService _fileService;
    private readonly NotificationAppService _notificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAvatarService _avatarService;
    private readonly ILogger<NotificationsController> _logger;
    private readonly string _uploadsFolder;

    public NotificationsController(
        NotificationAppService notificationService,
        INotificationRepository notificationRepository,
        ICurrentUserService currentUserService, 
        IMapper mapper, 
        IWebHostEnvironment environment, 
        FileService fileService, 
        IConfiguration configuration, 
        IServiceProvider serviceProvider, 
        ILogger<NotificationsController> logger,
        IAvatarService avatarService)
    {
        _notificationService = notificationService;
        _notificationRepository = notificationRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _fileService = fileService;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _avatarService = avatarService;

        // Определяем папку для загрузок относительно корня приложения
        _uploadsFolder = Path.Combine(environment.ContentRootPath, "uploads", "Notifications");
        
        // Создаем папку если не существует
        if (!Directory.Exists(_uploadsFolder))
        {
            Directory.CreateDirectory(_uploadsFolder);
        }
    }
    
    [HttpPost]
    [Authorize(Roles = "Teacher,Administrator")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateNotification([FromForm] CreateNotificationDto request)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        
        try
        {
            string? imgUrl = null;
            if (request.Image != null)
            {
                imgUrl = await _fileService.SaveImageAsync(request.Image, _uploadsFolder);
            }
            await _notificationService.SendNotificationAsync(request, _currentUserService.UserId.Value, imgUrl);
            return Ok(new { message = "Уведомление успешно отправлено" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (GroupNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
        }
    }
    
    [HttpPut("{id}/update")]
    [Authorize(Roles = "Teacher,Administrator")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateNotification(Guid id, [FromForm] UpdateNotificationDto request)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        
        try
        {
            var notification = await _notificationRepository.GetByIdWithReceiversAsync(id);
            if (notification == null) return BadRequest(new {message="Уведомление не найдено", success=false});
            
            string? imgUrl = null;
            // Если изображение передается в запросе
            if (request.Image != null)
            {
                // Если уведомление имеет изображение - удаляем
                if (!string.IsNullOrEmpty(notification.ImageUrl))
                {
                    await _fileService.DeleteImageAsync(notification.ImageUrl, _uploadsFolder);
                }
                // Добавляем новое изображение уведомлению если они не одинаковые
                var fileName1 = request.Image.FileName;
                var fileName2 = Path.GetFileName(notification.ImageUrl);
                if (!_fileService.IsImagesEquals(fileName1, fileName2 ?? string.Empty))
                {
                    imgUrl = await _fileService.SaveImageAsync(request.Image, _uploadsFolder);
                }

            }

            await _notificationService.UpdateNotificationAsync(notification, request, _currentUserService.UserId.Value, imgUrl);
            return Ok(new { message = "Уведомление успешно обновлено", success=true});
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, success=false });
        }
        catch (GroupNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера", success=false });
        }
    }
    
    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications([FromQuery] FilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        Guid currentUserId = _currentUserService.UserId.Value;
        
        var pageResult = await _notificationRepository.GetUserNotificationsAsync(currentUserId,
            _mapper.Map<FilterEntity>(query));

        var items = new List<NotificationDto>();
        foreach (var n in pageResult.Items)
        {
            items.Add(new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                SenderName = $"{n.Sender.FirstName} {n.Sender.LastName}",
                SenderRole = n.Sender.Role.ToString(),
                SenderAvatarUrl = await _avatarService.GetAvatarUrl(n.Sender.AvatarPresetKey),
                SenderId = n.SenderId,
                IsPersonal = NotificationUtils
                    .IsPersonal(n.Receivers.Select(r => r.UserId).ToHashSet(), currentUserId),
                IsFavorite = n.IsFavorite(currentUserId),
                CreatedAt = n.CreatedAt,
                AllowReplies = n.AllowReplies,
                IsRead = n.Receivers.FirstOrDefault(r => r.UserId == currentUserId)?.IsRead ?? false,
                ImageUrl = !string.IsNullOrEmpty(n.ImageUrl) ? $"{_configuration["BaseUrl"]}{n.ImageUrl}" : null
            });
        }
        
        var result = new GetItemsDto<NotificationDto>()
        {
            Items = items,
            TotalCount = pageResult.TotalCount,
            Page = pageResult.Page,
            PageSize = pageResult.PageSize
        };
        
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetNotificationById(Guid id)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        Guid currentUserId = _currentUserService.UserId.Value;
        
        var notification = await _notificationRepository.GetByIdAsync(id);
        if (notification == null)
            return NotFound(new { message = "Уведомление не найдено" });

        var result = new NotificationDetailDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            SenderName = $"{notification.Sender.FirstName} {notification.Sender.LastName}",
            SenderRole = notification.Sender.Role.ToString(),
            SenderAvatarUrl = await _avatarService.GetAvatarUrl(notification.Sender.AvatarPresetKey),
            SenderId = notification.SenderId,
            AllowReplies = notification.AllowReplies,
            IsPersonal = NotificationUtils
                .IsPersonal(notification.Receivers.Select(r => r.UserId).ToHashSet(), currentUserId),
            IsFavorite = notification.IsFavorite(currentUserId),
            CreatedAt = notification.CreatedAt,
            Receivers = notification.Receivers.Select(r => new NotificationReceiverDto
            {
                UserId = r.UserId,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                Role = r.User.Role,
                IsRead = r.IsRead
            }).ToList(),
            ImageUrl = !string.IsNullOrEmpty(notification.ImageUrl) ? $"{_configuration["BaseUrl"]}{notification.ImageUrl}" : null
        };

        return Ok(result);
    }
    
    [HttpPut("{id}/mark-as-read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var notification = await _notificationRepository.GetByIdAsync(id);
        if (notification == null)
            return NotFound(new { message = "Уведомление не найдено" });

        var receiver = notification.Receivers.FirstOrDefault(r => r.UserId == _currentUserService.UserId.Value);
        if (receiver == null)
            return Forbid("Нет доступа к этому уведомлению");

        if (!receiver.IsRead)
        {
            receiver.IsRead = true;
            await _notificationRepository.UpdateAsync(notification);
        }

        return Ok(new { message = "Уведомление помечено как прочитанное" });
    }

    [HttpGet("sent")]
    [Authorize(Roles = "Teacher,Administrator")]
    public async Task<IActionResult> GetSentNotifications([FromQuery] FilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();
        Guid currentUserId = _currentUserService.UserId.Value;
        
        var pagedResult = await _notificationRepository.GetBySenderIdAsync(_currentUserService.UserId.Value, 
            _mapper.Map<FilterEntity>(query));
        
        var sentNotifications = pagedResult
            .Items
            .Select(n => new SentNotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                CreatedAt = n.CreatedAt,
                IsPersonal = NotificationUtils
                    .IsPersonal(n.Receivers.Select(r => r.UserId).ToHashSet(), currentUserId),
                AllowReplies = n.AllowReplies,
                TotalReceivers = n.Receivers.Count(r => r.UserId != currentUserId),
                ReadReceivers = n.Receivers.Count(r => r.IsRead && r.UserId != currentUserId),
                ImageUrl = !string.IsNullOrEmpty(n.ImageUrl) ? $"{_configuration["BaseUrl"]}{n.ImageUrl}" : null
            }).ToList();

        var result = new GetItemsDto<SentNotificationDto>()
        {
            Items = sentNotifications,
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount
        };
        return Ok(result);
    }

    [HttpDelete("{id}/remove")]
    [Authorize(Roles = "Teacher,Administrator")]
    public async Task<IActionResult> RemoveNotification(Guid id)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var notification = await _notificationRepository.GetByIdAsync(id);
        if (notification == null)
            return NotFound(new { message = "Уведомление не найдено" });

        try
        {
            // Удаляем изображения если имеются
            if (notification.ImageUrl != null)
            {
                await _fileService.DeleteImageAsync(notification.ImageUrl, _uploadsFolder);
            }
            await _notificationRepository.DeleteAsync(notification);
            
            // уведомляем получателей об удалении
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            var receivers = notification.Receivers.Select(r => r.UserId).ToList();
            foreach (var userId in receivers)
            {
                try
                {
                    await hubContext.Clients.Group($"user_{userId}")
                        .SendAsync("RemovedNotification", notification.Id);
                }
                catch
                {
                    _logger.LogDebug("Failed to sent removed notification to user {UserId}", userId);
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "Не удалось удалить уведомление"
            });
        }

        return Ok(new {Message = "уведомление успешно удалено"});
    }
}
    