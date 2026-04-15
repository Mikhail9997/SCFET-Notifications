using Application.DTOs;
using Application.Hubs;
using AutoMapper;
using Core.Dtos;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public interface IChannelMessageService
{
    Task<PagedResult<ChannelMessageDto>> GetChannelMessagesAsync(Guid channelId, Guid userId, MessageFilterDto filter);
    Task<ChannelMessageDto> SendMessageAsync(Guid channelId, Guid senderId, SendMessageDto dto);
    Task<ChannelMessageDto> UpdateMessageAsync(Guid messageId, Guid userId, UpdateMessageDto dto);
    Task DeleteMessageAsync(Guid messageId, Guid userId);
    Task MarkAsReadAsync(Guid messageId, Guid userId);
    Task MarkAllAsReadAsync(Guid channelId, Guid userId);
    Task<int> GetUnreadCountAsync(Guid channelId, Guid userId);
}

public class ChannelMessageService:IChannelMessageService
{
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IChannelUserRepository _channelUserRepository;
    private readonly FileService _fileService;
    private readonly IAvatarService _avatarService;
    private readonly IWebHostEnvironment _environment;
    private readonly IMapper _mapper;
    private readonly IHubContext<ChannelHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly string _uploadsFolder;

    public ChannelMessageService(
        IChannelMessageRepository messageRepository,
        IChannelRepository channelRepository,
        IChannelUserRepository channelUserRepository,
        FileService fileService,
        IAvatarService avatarService,
        IWebHostEnvironment environment,
        IMapper mapper,
        IHubContext<ChannelHub> hubContext,
        IConfiguration configuration)
    {
        _messageRepository = messageRepository;
        _channelRepository = channelRepository;
        _channelUserRepository = channelUserRepository;
        _fileService = fileService;
        _avatarService = avatarService;
        _environment = environment;
        _mapper = mapper;
        _hubContext = hubContext;
        _configuration = configuration;

        _uploadsFolder = Path.Combine(_environment.ContentRootPath, "uploads", "channelMessages");
        if (!Directory.Exists(_uploadsFolder))
        {
            Directory.CreateDirectory(_uploadsFolder);
        }
    }


    public async Task<PagedResult<ChannelMessageDto>> GetChannelMessagesAsync(
        Guid channelId, Guid userId, MessageFilterDto filter)
    {
        var isMember = await _channelUserRepository.IsUserInChannelAsync(channelId, userId);
        if (!isMember)
        {
            throw new InvalidOperationException("Вы не являетесь участником этого канала");
        }

        var pagedMessages = await _messageRepository
            .GetChannelMessagesAsync(channelId, filter);
        
        var messageDtos = new List<ChannelMessageDto>();
        
        foreach (var message in pagedMessages.Items)
        {
            var dto = await MapToDtoAsync(message, userId);
            messageDtos.Add(dto);
        }

        return new PagedResult<ChannelMessageDto>
        {
            Items = messageDtos,
            TotalCount = pagedMessages.TotalCount,
            Page = pagedMessages.Page,
            PageSize = pagedMessages.PageSize
        };
    }


    public async Task<ChannelMessageDto> SendMessageAsync(Guid channelId, Guid senderId, SendMessageDto dto)
    {
        var channel = await _channelRepository.GetByIdAsync(channelId);
        if (channel == null)
        {
            throw new InvalidOperationException("Канал не найден или деактивирован");
        }

        var isMember = await _channelUserRepository.IsUserInChannelAsync(channelId, senderId);
        if (!isMember)
        {
            throw new InvalidOperationException("Вы не являетесь участником этого канала");
        }

        if (dto.ReplyToMessageId.HasValue)
        {
            var replyMessage = await _messageRepository.GetByIdAsync(dto.ReplyToMessageId.Value);
            if (replyMessage == null || replyMessage.ChannelId != channelId)
            {
                throw new InvalidOperationException("Сообщение для ответа не найдено");
            }
        }

        var message = new ChannelMessage
        {
            Content = dto.Content,
            ChannelId = channelId,
            SenderId = senderId,
            ReplyToMessageId = dto.ReplyToMessageId
        };

        // Сохраняем изображение если есть 
        if (dto.Image != null)
        {
            string imagePath = await _fileService.SaveImageAsync(dto.Image, _uploadsFolder);
            message.ImageUrl = _configuration["BaseUrl"] + imagePath;
        }

        await _messageRepository.AddAsync(message);

        message = await _messageRepository.GetMessageWithDetailsAsync(message.Id);
        
        var dtoResult = await MapToDtoAsync(message!, senderId);

        await _hubContext.Clients.Group($"channel_{channelId}")
            .SendAsync("NewMessage", dtoResult);

        return dtoResult;
    }

    public async Task<ChannelMessageDto> UpdateMessageAsync(Guid messageId, Guid userId, UpdateMessageDto dto)
    {
        var message = await _messageRepository.GetMessageWithDetailsAsync(messageId);
        
        if (message == null)
        {
            throw new InvalidOperationException("Сообщение не найдено");
        }

        if (message.SenderId != userId)
        {
            throw new InvalidOperationException("Вы не можете редактировать это сообщение");
        }

        message.Content = dto.Content;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _messageRepository.UpdateAsync(message);
        
        var dtoResult = await MapToDtoAsync(message, userId);

        await _hubContext.Clients.Group($"channel_{message.ChannelId}")
            .SendAsync("MessageUpdated", dtoResult);

        return dtoResult;
    }

    public async Task DeleteMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _messageRepository.GetMessageWithDetailsAsync(messageId);
        
        if (message == null)
        {
            throw new InvalidOperationException("Сообщение не найдено");
        }

        var canDelete = await _messageRepository.CanUserModifyMessageAsync(messageId, userId);
        if (!canDelete)
        {
            var reason = await _messageRepository.GetDeleteDenyReasonAsync(messageId, userId);
            throw new InvalidOperationException(reason);
        }

        var channelId = message.ChannelId;
        
        // Удаляем изображение если есть
        if (!string.IsNullOrEmpty(message.ImageUrl))
        {
            await _fileService.DeleteImageAsync(message.ImageUrl, _uploadsFolder);
        }
        
        await _messageRepository.DeleteAsync(message);

        await _hubContext.Clients.Group($"channel_{channelId}")
            .SendAsync("MessageDeleted", messageId);
    }

    public async Task MarkAsReadAsync(Guid messageId, Guid userId)
    {
        await _messageRepository.MarkAsReadAsync(messageId, userId);
    }

    public async Task MarkAllAsReadAsync(Guid channelId, Guid userId)
    {
        await _messageRepository.MarkAllAsReadAsync(channelId, userId);
    }

    public async Task<int> GetUnreadCountAsync(Guid channelId, Guid userId)
    {
        return await _messageRepository.GetUnreadCountAsync(channelId, userId);
    }
    
   private async Task<ChannelMessageDto> MapToDtoAsync(
        ChannelMessage message, 
        Guid currentUserId)
    {
        var dto = _mapper.Map<ChannelMessageDto>(message);
        
        dto.SenderName = $"{message.Sender.LastName} {message.Sender.FirstName}".Trim();
        dto.SenderAvatar = await _avatarService.GetAvatarUrl(message.Sender.AvatarPresetKey);
        dto.SenderRole = message.Sender.Role;
        dto.SenderChannelRole = await _channelUserRepository.GetUserRoleInChannelAsync(message.ChannelId, message.SenderId);
        
        dto.CanEdit = message.SenderId == currentUserId;
        dto.CanDelete = await _messageRepository.CanUserModifyMessageAsync(message.Id, currentUserId);

        if (message.ReplyToMessage != null)
        {
            dto.ReplyToMessage = new ReplyMessageDto
            {
                Id = message.ReplyToMessage.Id,
                Content = message.ReplyToMessage.Content,
                SenderId = message.ReplyToMessage.SenderId,
                SenderName = $"{message.ReplyToMessage.Sender.LastName} {message.ReplyToMessage.Sender.FirstName}".Trim(),
                SenderAvatar = await _avatarService.GetAvatarUrl(message.ReplyToMessage.Sender.AvatarPresetKey),
                ImageUrl = message.ReplyToMessage.ImageUrl,
                CreatedAt = message.ReplyToMessage.CreatedAt
            };
        }

        return dto;
    }
}