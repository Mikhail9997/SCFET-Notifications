using System.Security.Authentication;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Exceptions;
using Application.Hubs;
using Application.Messages.Kafka;
using Core.Interfaces;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class NotificationAppService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationAppService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public NotificationAppService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IKafkaProducer kafkaProducer, 
        IServiceProvider serviceProvider, 
        ILogger<NotificationAppService> logger, ApplicationDbContext context, IConfiguration configuration)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _kafkaProducer = kafkaProducer;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _context = context;
        _configuration = configuration;
    }
    
    public async Task SendNotificationAsync(CreateNotificationDto dto, Guid senderId, string? imageUrl = null)
    {
        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null) throw new UnauthorizedAccessException("User not found");

        // Проверка разрешений на основе роли
        await ValidateNotificationPermissionsAsync(sender.Role, dto);

        // Берем целевых пользователей
        var recipients = await GetRecipientsAsync(dto, sender.Role);
        
        if (!recipients.Any())
            throw new InvalidOperationException("Получатели уведомления не найдены");

        // Создать уведомление
        var notification = new Notification
        {
            Title = dto.Title,
            Message = dto.Message,
            AllowReplies = dto.AllowReplies,
            Type = dto.Type,
            SenderId = senderId,
            ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl
        };

        // Добавление получателей
        foreach (var user in recipients)
        {
            notification.Receivers.Add(new NotificationReceiver
            {
                UserId = user.Id,
                IsRead = user.Id == senderId // отправитель сразу "прочитает" сообщение
            });
        }

        await _notificationRepository.AddAsync(notification);

        // Отправляем в Kafka
        var kafkaMessage = new NotificationKafkaMessage
        {
            NotificationId = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            SenderName = $"{sender.FirstName} {sender.LastName}",
            SenderRole = notification.Sender.Role.ToString(),
            SenderId = senderId,
            AllowReplies = notification.AllowReplies,
            RecipientUserIds = recipients.Select(r => r.Id).ToList(),
            CreatedAt = notification.CreatedAt,
            ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl
        };

        var messageJson = JsonSerializer.Serialize(kafkaMessage);
        await _kafkaProducer.ProduceAsync("notifications.all", messageJson);
    }

    public async Task UpdateNotificationAsync(Notification notification, UpdateNotificationDto dto, Guid senderId, string? imageUrl = null)
    {
        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null) throw new UnauthorizedAccessException("User not found");
        
        // Загружаем notification с получателями 
        var existingNotification = await _context.Notifications
            .Include(n => n.Receivers)
            .FirstOrDefaultAsync(n => n.Id == notification.Id);
            
        if (existingNotification == null)
            throw new InvalidOperationException("Уведомление не найдено");
        
        // Обновляем основные поля
        existingNotification.Title = dto.Title;
        existingNotification.Message = dto.Message;
        existingNotification.AllowReplies = dto.AllowReplies;
        existingNotification.Type = dto.Type;
        existingNotification.ImageUrl = imageUrl ?? existingNotification.ImageUrl;
        
        // Получаем новых получателей
        var newReceivers = await GetRecipientsAsync(dto, sender.Role);
        if (!newReceivers.Any())
            throw new InvalidOperationException("Получатели уведомления не найдены");
        
        var newReceiverIds = newReceivers.Select(r => r.Id).ToHashSet();
        
        var existingReceiverIds = existingNotification.Receivers
            .Select(r => r.UserId)
            .ToHashSet();
        
        // Обрабатываем получателей
        
        // Удаляем тех, кого больше нет
        var receiversToRemove = existingNotification.Receivers
            .Where(r => !newReceiverIds.Contains(r.UserId))
            .ToHashSet();
            
        foreach (var receiver in receiversToRemove)
        {
            _context.NotificationReceivers.Remove(receiver);
        }
        
        // Определяем существующих получателей, которые остаются (не удалены и не новые)
        var existingReceiversThatStay = existingReceiverIds
            .Where(id => newReceiverIds.Contains(id))
            .ToHashSet();
        
        // Добавляем новых получателей
            
        foreach (var userId in newReceiverIds)
        {
            if (!existingReceiverIds.Contains(userId))
            {
                var newReceiver = new NotificationReceiver
                {
                    NotificationId = existingNotification.Id,
                    UserId = userId,
                    IsRead = userId == senderId // отправитель сразу "прочитает" сообщение
                };
                _context.NotificationReceivers.Add(newReceiver);
            }
        }
        
        // Сохраняем изменения
        await _context.SaveChangesAsync();
        
        // Отправляем в Kafka если получатели изменились
        if (receiversToRemove.Any() || newReceiverIds.Except(existingReceiverIds).Any())
        {
            var kafkaMessage = new NotificationKafkaMessage
            {
                NotificationId = existingNotification.Id,
                Title = existingNotification.Title,
                Message = existingNotification.Message,
                Type = existingNotification.Type,
                SenderName = $"{sender.FirstName} {sender.LastName}",
                SenderRole = existingNotification.Sender.Role.ToString(),
                SenderId = senderId,
                AllowReplies = existingNotification.AllowReplies,
                RecipientUserIds = newReceiverIds.ToList(),
                CreatedAt = existingNotification.CreatedAt,
                ImageUrl = existingNotification.ImageUrl
            };
            
            var messageJson = JsonSerializer.Serialize(kafkaMessage);
            await _kafkaProducer.ProduceAsync("notifications.all", messageJson);

            var receiversToRemoveIds = receiversToRemove
                .Select(r => r.UserId).ToHashSet();
            
            await NotifyRemovedReceiversAboutUpdate(existingNotification.Id, receiversToRemoveIds);
        }
        
        // Отправляем уведомление через SignalR для тех, у кого изменилось уведомление
        await NotifyReceiversAboutUpdate(existingNotification.Id, existingNotification, existingReceiversThatStay, newReceiverIds, sender);
    }
    
    private async Task NotifyReceiversAboutUpdate(Guid notificationId, Notification notification, HashSet<Guid> receiverThatStayIds, HashSet<Guid> newReceiverIds, User sender)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            foreach (var receiverId in receiverThatStayIds)
            {
                try
                {
                    await hubContext.Clients.Group($"user_{receiverId}")
                        .SendAsync("UpdateNotification", new NotificationDto
                        {
                            Id = notificationId,
                            Title = notification.Title,
                            Message = notification.Message,
                            Type = notification.Type.ToString(),
                            SenderName = $"{sender.FirstName} {sender.LastName}",
                            SenderRole = notification.Sender.Role.ToString(),
                            SenderId = notification.SenderId,
                            AllowReplies = notification.AllowReplies,
                            IsPersonal = newReceiverIds.Count == 1 && newReceiverIds.Contains(receiverId),
                            CreatedAt = notification.CreatedAt,
                            IsRead = notification.Receivers
                                .FirstOrDefault(r => r.UserId == receiverId)!.IsRead,
                            ImageUrl = !string.IsNullOrEmpty(notification.ImageUrl) ? $"{_configuration["CloudPud:Ip"]}{notification.ImageUrl}" : null
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send updated notification to user {UserId}", receiverId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }

    private async Task NotifyRemovedReceiversAboutUpdate(Guid notificationId, 
        HashSet<Guid> receiverIds)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            foreach (var receiverId in receiverIds)
            {
                try
                {
                    await hubContext.Clients.Group($"user_{receiverId}")
                        .SendAsync("RemovedNotification", notificationId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send updated notification to user {UserId}", receiverId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }
    
    private async Task ValidateNotificationPermissionsAsync(UserRole senderRole, CreateNotificationDto dto)
    {
        if (senderRole == UserRole.Teacher)
        {
            // Учителя могут отправлять сообщения только учащимся и другим учителям
            if (dto.TargetUserIds != null && dto.TargetUserIds.Any())
            {
                var targetUsers = (await _userRepository.GetUsersByRoleAsync(UserRole.Student))
                    .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Parent))
                    .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Teacher));
                var invalidUsers = dto.TargetUserIds.Except(targetUsers.Select(u => u.Id));
                if (invalidUsers.Any())
                    throw new UnauthorizedAccessException("Учителя могут отправлять уведомления только учащимся и учителям");
            }
            
            // Групповая проверка - преподаватели могут отправлять сообщения только группам учащихся.
            if (dto.TargetGroupId.HasValue)
            {
                var group = await _groupRepository.GetByIdAsync(dto.TargetGroupId.Value);
                if (group == null)
                    throw new ArgumentException("Группа не найдена");
            }
        }
        // Администраторы могут отправлять сообщения кому угодно - никаких ограничений
    }
    
    private async Task<List<User>> GetRecipientsAsync(NotificationActionDto dto, UserRole senderRole)
    {
        if (dto.TargetUserIds != null && dto.TargetUserIds.Any())
        {
            var recipients = new List<User>();
            foreach (var userId in dto.TargetUserIds)
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null) recipients.Add(user);
            }
            return recipients;
        }

        if (dto.TargetGroupId.HasValue)
        {
            var group = await _groupRepository.GetByIdAsync(dto.TargetGroupId.Value);
            if (group == null) throw new GroupNotFoundException("Группа не найдена");
            return (await _userRepository.GetUsersByGroupAsync(dto.TargetGroupId.Value)).ToList();
        }

        // Если конкретных целей нет, отправляем всем разрешенным пользователям в зависимости от роли
        return senderRole switch
        {
            // Если учитель - отправка всем студентам и учителям и родителям
            UserRole.Teacher => (await _userRepository.GetUsersByRoleAsync(UserRole.Student))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Parent))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Teacher))
                .ToList(),
            // Если админ - отправка всем студентам и учителям, и администраторам, и родителям
            UserRole.Administrator => (await _userRepository.GetUsersByRoleAsync(UserRole.Student))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Parent))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Teacher))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Administrator))
                .ToList(),
            _ => throw new UnauthorizedAccessException("Недопустимая роль пользователя для отправки уведомлений")
        };
    }
}