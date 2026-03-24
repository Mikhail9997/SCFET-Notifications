using Application.DTOs;
using Application.Hubs;
using AutoMapper;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class NotificationReplyService
{
    private readonly INotificationReplyRepository _notificationReplyRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationReplyService> _logger;
    private readonly IMapper _mapper;
        
    public NotificationReplyService(INotificationReplyRepository notificationReplyRepository, 
        ILogger<NotificationReplyService> logger, INotificationRepository notificationRepository, 
        IUserRepository userRepository, IServiceProvider serviceProvider, IMapper mapper)
    {
        _notificationReplyRepository = notificationReplyRepository;
        _logger = logger;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _serviceProvider = serviceProvider;
        _mapper = mapper;
    }
    
    public async Task SendNotificationReplyAsync(CreateReplyDto dto, Guid senderId)
    {
        Notification? notification = await _notificationRepository
            .GetByIdWithReceiversAsync(dto.NotificationId);

        if (notification == null)
        {
            throw new InvalidOperationException("Уведомление не найдено");
        }
        
        if (!notification.AllowReplies)
            throw new InvalidOperationException("Ответы на это уведомление запрещены");
        
        var reply = new NotificationReply
        {
            Id = Guid.NewGuid(),
            NotificationId = dto.NotificationId,
            UserId = senderId,
            Message = dto.Message,
            CreatedAt = DateTime.UtcNow,
        };

        await _notificationReplyRepository.AddAsync(reply);

        var receiversIds = notification.Receivers
            .Select(r => r.UserId)
            .ToList();
        
        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null)
        {
            throw new InvalidOperationException("Отправитель не найден");
        }
        
        // Отправляем уведомление отправителю через SignalR
        await NotifyReceiversAboutSend(receiversIds, reply, sender);
    }

    public async Task UpdateNotificationReplyAsync(Guid id, UpdateNotificationReplyDto dto)
    {
        var notificationReply = await _notificationReplyRepository.GetByIdAsync(id);
        if (notificationReply == null)
        {
            throw new InvalidOperationException("ответ не найден");
        }

        notificationReply.Message = dto.Message;
        notificationReply.UpdatedAt = DateTime.UtcNow;
        await _notificationReplyRepository.UpdateAsync(notificationReply);
        
        var receiversIds = notificationReply.Notification.Receivers
            .Select(r => r.UserId)
            .ToList();

        ReplyDto reply = _mapper.Map<ReplyDto>(notificationReply);
        
        // уведомляем получателей об обновлении через SignalR
        await NotifyReceiversAboutUpdate(receiversIds, reply);
    }
    
    public async Task RemoveNotificationReplyAsync(Guid id)
    {
        var notificationReply = await _notificationReplyRepository.GetByIdAsync(id);
        if (notificationReply == null)
        {
            throw new InvalidOperationException("ответ не найден");
        }

        var receiversIds = notificationReply.Notification.Receivers
            .Select(r => r.UserId)
            .ToList();
        await _notificationReplyRepository.DeleteAsync(notificationReply);
        
        // уведомляем получателей об удалении через SignalR
        await NotifyReceiversAboutRemove(receiversIds, notificationReply.Id);
    }
    
    private async Task NotifyReceiversAboutSend(List<Guid> receiverIds, NotificationReply reply, User sender)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            foreach (Guid receiverId in receiverIds)
            {
                try
                {
                    ReplyDto replyDto = new ReplyDto()
                    {
                        Id = reply.Id,
                        NotificationId = reply.NotificationId,
                        UserId = reply.UserId,
                        UserName = $"{sender.FirstName} {sender.LastName}",
                        UserRole = sender.Role.ToString(),
                        Message = reply.Message,
                        CreatedAt = reply.CreatedAt
                    };
                    await hubContext.Clients.Group($"user_{receiverId}")
                        .SendAsync("ReceiveNotificationReply", replyDto);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send notification reply to user {UserId}", receiverId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }
    
    private async Task NotifyReceiversAboutRemove(List<Guid> receiverIds, Guid notificationReplyId)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            foreach (Guid receiverId in receiverIds)
            {
                try
                {
                    await hubContext.Clients.Group($"user_{receiverId}")
                        .SendAsync("RemoveNotificationReply", notificationReplyId);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send remove notification reply to user {UserId}", receiverId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }
    
    private async Task NotifyReceiversAboutUpdate(List<Guid> receiverIds, ReplyDto reply)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            foreach (Guid receiverId in receiverIds)
            {
                try
                {
                    await hubContext.Clients.Group($"user_{receiverId}")
                        .SendAsync("UpdateNotificationReply", reply);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send update notification reply to user {UserId}", receiverId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }
}