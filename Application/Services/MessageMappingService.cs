using Application.DTOs;
using AutoMapper;
using Core.Interfaces;
using Core.Models;

namespace Application.Services;

public interface IMessageMappingService
{
    Task<PagedResult<ChannelMessageDto>> MapToPagedDtoAsync(PagedResult<ChannelMessage> messages, Guid channelId, Guid currentUserId);
    Task<ChannelMessageDto> MapToDtoAsync(ChannelMessage message, Guid channelId, Guid currentUserId);
    Task<List<ChannelMessageDto>> MapToListDtoAsync(List<ChannelMessage> messages, Guid channelId, Guid currentUserId);
}

public class MessageMappingService : IMessageMappingService
{
    private readonly IAvatarService _avatarService;
    private readonly IChannelUserRepository _channelUserRepository;
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    // Кэш для одного запроса
    private readonly Dictionary<Guid, string> _avatarUrlCache = new();
    private readonly Dictionary<Guid, ChannelRole?> _channelRoleCache = new();
    private readonly Dictionary<Guid, bool> _canModifyCache = new();
    private readonly Dictionary<Guid, string> _userNameCache = new();

    public MessageMappingService(
        IAvatarService avatarService,
        IChannelUserRepository channelUserRepository,
        IChannelMessageRepository messageRepository,
        IUserRepository userRepository,
        IMapper mapper)
    {
        _avatarService = avatarService;
        _channelUserRepository = channelUserRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _mapper = mapper;
    }

    #region Public Methods

    public async Task<PagedResult<ChannelMessageDto>> MapToPagedDtoAsync(
        PagedResult<ChannelMessage> messages, Guid channelId, Guid currentUserId)
    {
        ClearCache();
        await PreloadDataAsync((List<ChannelMessage>)messages.Items, channelId, currentUserId);

        var dtos = new List<ChannelMessageDto>(messages.Items.Count);
        foreach (var message in messages.Items)
        {
            dtos.Add(MapSingleMessage(message, currentUserId));
        }

        return new PagedResult<ChannelMessageDto>
        {
            Items = dtos,
            TotalCount = messages.TotalCount,
            Page = messages.Page,
            PageSize = messages.PageSize
        };
    }

    public async Task<List<ChannelMessageDto>> MapToListDtoAsync(
        List<ChannelMessage> messages, Guid channelId, Guid currentUserId)
    {
        ClearCache();
        await PreloadDataAsync(messages, channelId, currentUserId);

        var dtos = new List<ChannelMessageDto>(messages.Count);
        foreach (var message in messages)
        {
            dtos.Add(MapSingleMessage(message, currentUserId));
        }

        return dtos;
    }

    public async Task<ChannelMessageDto> MapToDtoAsync(
        ChannelMessage message, Guid channelId, Guid currentUserId)
    {
        ClearCache();
        await PreloadDataAsync(new List<ChannelMessage> { message }, channelId, currentUserId);
        return MapSingleMessage(message, currentUserId);
    }

    #endregion

    #region Preloading

    private void ClearCache()
    {
        _avatarUrlCache.Clear();
        _channelRoleCache.Clear();
        _canModifyCache.Clear();
        _userNameCache.Clear();
    }

    private async Task PreloadDataAsync(List<ChannelMessage> messages, Guid channelId, Guid currentUserId)
    {
        var userIds = CollectUserIds(messages);
    
        // Последовательная загрузка из БД
        var users = await _userRepository.GetByIdsAsync(userIds);
        var roles = await _channelUserRepository.GetUsersRolesInChannelAsync(channelId, userIds);
        var rights = await _messageRepository.GetModifyRightsAsync(channelId, messages.Select(m => m.Id), currentUserId);

        // загрузка аватарок
        var presetKeys = users
            .Select(u => u.AvatarPresetKey)
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct();
    
        var avatarUrls = await _avatarService.GetAvatarUrlsAsync(presetKeys!);

        // Заполняем кэши
        foreach (var user in users)
        {
            _userNameCache[user.Id] = FormatUserName(user);
        
            if (!string.IsNullOrEmpty(user.AvatarPresetKey) && avatarUrls.TryGetValue(user.AvatarPresetKey, out var url))
            {
                _avatarUrlCache[user.Id] = url;
            }
            else
            {
                _avatarUrlCache[user.Id] = "/uploads/avatars/presets/default/default.jpg";
            }
        }

        // Fallback для отсутствующих
        foreach (var id in userIds)
        {
            if (!_userNameCache.ContainsKey(id))
            {
                _userNameCache[id] = "Неизвестный";
                _avatarUrlCache[id] = "/uploads/avatars/presets/default/default.jpg";
            }
        }

        foreach (var role in roles)
        {
            _channelRoleCache[role.UserId] = role.Role;
        }

        foreach (var right in rights)
        {
            _canModifyCache[right.MessageId] = right.CanDelete;
        }
    }

    private static HashSet<Guid> CollectUserIds(List<ChannelMessage> messages)
    {
        var userIds = new HashSet<Guid>();
        
        foreach (var msg in messages)
        {
            userIds.Add(msg.SenderId);
            if (msg.ReplyToMessage?.SenderId != null)
                userIds.Add(msg.ReplyToMessage.SenderId);
        }

        return userIds;
    }

    #endregion

    #region Mapping

    private ChannelMessageDto MapSingleMessage(ChannelMessage message, Guid currentUserId)
    {
        var dto = _mapper.Map<ChannelMessageDto>(message);

        // Отправитель
        dto.SenderName = _userNameCache.GetValueOrDefault(message.SenderId, "Неизвестный");
        dto.SenderAvatar = _avatarUrlCache.GetValueOrDefault(message.SenderId, "/uploads/avatars/presets/default/default.jpg");
        dto.SenderRole = message.Sender.Role;
        dto.SenderChannelRole = _channelRoleCache.GetValueOrDefault(message.SenderId);

        // Права
        dto.CanEdit = message.SenderId == currentUserId;
        dto.CanDelete = _canModifyCache.GetValueOrDefault(message.Id);

        // Ответ
        if (message.ReplyToMessage != null)
        {
            dto.ReplyToMessage = MapReplyMessage(message.ReplyToMessage);
        }

        return dto;
    }

    private ReplyMessageDto MapReplyMessage(ChannelMessage reply)
    {
        return new ReplyMessageDto
        {
            Id = reply.Id,
            Content = reply.Content,
            SenderId = reply.SenderId,
            SenderName = _userNameCache.GetValueOrDefault(reply.SenderId, "Неизвестный"),
            SenderAvatar = _avatarUrlCache.GetValueOrDefault(reply.SenderId, "/uploads/avatars/presets/default/default.jpg"),
            ImageUrl = reply.ImageUrl,
            CreatedAt = reply.CreatedAt
        };
    }

    private static string FormatUserName(User user)
    {
        if (user == null) return "Неизвестный";
        return $"{user.LastName} {user.FirstName}".Trim();
    }

    #endregion
}