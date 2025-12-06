using System.Text.Json;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Messages.Kafka;
using Core.Interfaces;
using Core.Models;

namespace Application.Services;

public class NotificationAppService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ICurrentUserService _currentUserService;

    public NotificationAppService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IKafkaProducer kafkaProducer,
        ICurrentUserService currentUserService)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _kafkaProducer = kafkaProducer;
        _currentUserService = currentUserService;
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
                IsRead = false
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
            RecipientUserIds = recipients.Select(r => r.Id).ToList(),
            CreatedAt = notification.CreatedAt,
            ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl
        };

        var messageJson = JsonSerializer.Serialize(kafkaMessage);
        await _kafkaProducer.ProduceAsync("notifications.all", messageJson);
    }
    
    private async Task ValidateNotificationPermissionsAsync(UserRole senderRole, CreateNotificationDto dto)
    {
        if (senderRole == UserRole.Teacher)
        {
            // Учителя могут отправлять сообщения только учащимся и другим учителям
            if (dto.TargetUserIds != null && dto.TargetUserIds.Any())
            {
                var targetUsers = (await _userRepository.GetUsersByRoleAsync(UserRole.Student))
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
    
    private async Task<List<User>> GetRecipientsAsync(CreateNotificationDto dto, UserRole senderRole)
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
            return (await _userRepository.GetUsersByGroupAsync(dto.TargetGroupId.Value)).ToList();
        }

        // Если конкретных целей нет, отправляем всем разрешенным пользователям в зависимости от роли
        return senderRole switch
        {
            // Если учитель - отправка всем студентам и учителям
            UserRole.Teacher => (await _userRepository.GetUsersByRoleAsync(UserRole.Student))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Teacher))
                .ToList(),
            // Если админ - отправка всем студентам и учителям, и администраторам
            UserRole.Administrator => (await _userRepository.GetUsersByRoleAsync(UserRole.Student))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Teacher))
                .Concat(await _userRepository.GetUsersByRoleAsync(UserRole.Administrator))
                .ToList(),
            _ => throw new UnauthorizedAccessException("Недопустимая роль пользователя для отправки уведомлений")
        };
    }
}