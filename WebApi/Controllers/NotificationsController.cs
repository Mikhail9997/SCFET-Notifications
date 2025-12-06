using Application.Common.Interfaces;
using Application.DTOs;
using Application.Hubs;
using Application.Services;
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
    private readonly IWebHostEnvironment _environment;
    private readonly FileService _fileService;
    private readonly NotificationAppService _notificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly string _uploadsFolder;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationAppService notificationService,
        INotificationRepository notificationRepository,
        ICurrentUserService currentUserService, 
        IMapper mapper, 
        IWebHostEnvironment environment, 
        FileService fileService, 
        IConfiguration configuration, 
        IServiceProvider serviceProvider, 
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _notificationRepository = notificationRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _environment = environment;
        _fileService = fileService;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Определяем папку для загрузок относительно корня приложения
        _uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "Notifications");
        
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
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
        }
    }
    
    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications([FromQuery] NotificationFilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var pageResult = await _notificationRepository.GetUserNotificationsAsync(_currentUserService.UserId.Value,
            _mapper.Map<NotificationFilterEntity>(query));

        var result = new GetNotificationDto<NotificationDto>()
        {
            Items = pageResult.Items.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                SenderName = $"{n.Sender.FirstName} {n.Sender.LastName}",
                CreatedAt = n.CreatedAt,
                IsRead = n.Receivers.FirstOrDefault(r => r.UserId == _currentUserService.UserId.Value)?.IsRead ?? false,
                ImageUrl = !string.IsNullOrEmpty(n.ImageUrl) ? $"{_configuration["CloudPud:Ip"]}{n.ImageUrl}" : null
            }).ToList(),
            TotalCount = pageResult.TotalCount,
            Page = pageResult.Page,
            PageSize = pageResult.PageSize
        };
        
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNotificationById(Guid id)
    {
        var notification = await _notificationRepository.GetByIdAsync(id);
        if (notification == null)
            return NotFound(new { message = "Уведомление не найдено" });

        // Проверяем, имеет ли пользователь доступ к этому уведомлению
        if (!notification.Receivers.Any(r => r.UserId == _currentUserService.UserId.Value) &&
            notification.SenderId != _currentUserService.UserId.Value &&
            _currentUserService.Role != UserRole.Administrator)
        {
            return Forbid("Нет доступа к этому уведомлению");
        }

        var result = new NotificationDetailDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            SenderName = $"{notification.Sender.FirstName} {notification.Sender.LastName}",
            SenderId = notification.SenderId,
            CreatedAt = notification.CreatedAt,
            Receivers = notification.Receivers.Select(r => new NotificationReceiverDto
            {
                UserId = r.UserId,
                UserName = $"{r.User.FirstName} {r.User.LastName}",
                IsRead = r.IsRead
            }).ToList(),
            ImageUrl = !string.IsNullOrEmpty(notification.ImageUrl) ? $"{_configuration["CloudPud:Ip"]}{notification.ImageUrl}" : null
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
    public async Task<IActionResult> GetSentNotifications([FromQuery] NotificationFilterDto query)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var pagedResult = await _notificationRepository.GetBySenderIdAsync(_currentUserService.UserId.Value, 
            _mapper.Map<NotificationFilterEntity>(query));
        
        var sentNotifications = pagedResult
            .Items
            .Select(n => new SentNotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                CreatedAt = n.CreatedAt,
                TotalReceivers = n.Receivers.Count,
                ReadReceivers = n.Receivers.Count(r => r.IsRead),
                ImageUrl = !string.IsNullOrEmpty(n.ImageUrl) ? $"{_configuration["CloudPud:Ip"]}{n.ImageUrl}" : null
            }).ToList();

        var result = new GetNotificationDto<SentNotificationDto>()
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
            //Удаляем изображения если имеются
            if (notification.ImageUrl != null)
            {
                await _fileService.DeleteNotificationImagesAsync(notification.ImageUrl, _uploadsFolder);
            }
            await _notificationRepository.DeleteAsync(notification);
            
            //умедовляем получателей об удалении
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
    