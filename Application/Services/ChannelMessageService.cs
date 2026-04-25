using Application.DTOs;
using Application.Hubs;
using AutoMapper;
using Core.Dtos;
using Core.Dtos.Filters;
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
    Task MarkMessagesAsReadAsync(Guid channelId, List<Guid> messageIds, Guid userId);
    Task<int> GetUnreadCountAsync(Guid channelId, Guid userId);
}

public class ChannelMessageService : IChannelMessageService
{
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IChannelUserRepository _channelUserRepository;
    private readonly FileService _fileService;
    private readonly IMessageMappingService _mappingService;
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
        IMessageMappingService mappingService,
        IWebHostEnvironment environment,
        IMapper mapper,
        IHubContext<ChannelHub> hubContext,
        IConfiguration configuration)
    {
        _messageRepository = messageRepository;
        _channelRepository = channelRepository;
        _channelUserRepository = channelUserRepository;
        _fileService = fileService;
        _mappingService = mappingService;
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

        var pagedMessages = await _messageRepository.GetChannelMessagesAsync(channelId, filter);
        return await _mappingService.MapToPagedDtoAsync(pagedMessages, channelId, userId);
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
            Content = dto.Content ?? string.Empty,
            ChannelId = channelId,
            SenderId = senderId,
            ReplyToMessageId = dto.ReplyToMessageId
        };

        if (dto.Image != null)
        {
            string imagePath = await _fileService.SaveImageAsync(dto.Image, _uploadsFolder);
            message.ImageUrl = _configuration["BaseUrl"] + imagePath;
        }

        await _messageRepository.AddAsync(message);
        message = await _messageRepository.GetMessageWithDetailsAsync(message.Id);
        
        var dtoResult = await _mappingService.MapToDtoAsync(message!, channelId, senderId);

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
        
        var dtoResult = await _mappingService.MapToDtoAsync(message, message.ChannelId, userId);

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
        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null) return;
    
        await _messageRepository.MarkAsReadAsync(messageId, userId);
    
        await _hubContext.Clients.Group($"channel_{message.ChannelId}")
            .SendAsync("MessageRead", messageId, message.ChannelId);
    }

    public async Task MarkAllAsReadAsync(Guid channelId, Guid userId)
    {
        await _messageRepository.MarkAllAsReadAsync(channelId, userId);
    }

    public async Task MarkMessagesAsReadAsync(Guid channelId, List<Guid> messageIds, Guid userId)
    {
        if (messageIds == null || !messageIds.Any()) return;
    
        var isMember = await _channelUserRepository.IsUserInChannelAsync(channelId, userId);
        if (!isMember)
        {
            throw new InvalidOperationException("Вы не являетесь участником этого канала");
        }
    
        var markedCount = await _messageRepository.MarkMessagesAsReadAsync(
            channelId, messageIds.ToHashSet(), userId);
    
        if (markedCount > 0)
        {
            await _hubContext.Clients.Group($"channel_{channelId}")
                .SendAsync("MessagesRead", messageIds, channelId);
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid channelId, Guid userId)
    {
        return await _messageRepository.GetUnreadCountAsync(channelId, userId);
    }
}