using Application.DTOs;
using AutoMapper;
using Core.Dtos;
using Core.Dtos.Filters;
using Core.Interfaces;
using Core.Models;

namespace Application.Services;

public interface IChannelUserService
{
    Task<PagedResult<AvailableUserDto>> GetAvailableUsersForChannelAsync(
        Guid channelId, 
        Guid currentUserId, 
        AvailableUsersFilterDto filter);
}

public class ChannelUserService:IChannelUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IChannelUserRepository _channelUserRepository;
    private readonly IAvatarService _avatarService;
    private readonly IMapper _mapper;

    public ChannelUserService(
        IUserRepository userRepository,
        IChannelRepository channelRepository,
        IChannelUserRepository channelUserRepository,
        IAvatarService avatarService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _channelRepository = channelRepository;
        _channelUserRepository = channelUserRepository;
        _avatarService = avatarService;
        _mapper = mapper;
    }
    
    public async Task<PagedResult<AvailableUserDto>> GetAvailableUsersForChannelAsync(
        Guid channelId, 
        Guid currentUserId, 
        AvailableUsersFilterDto filter)
    {
        // Проверяем существование канала и права пользователя
        var channel = await _channelRepository.GetByIdAsync(channelId);
        if (channel == null)
        {
            throw new InvalidOperationException("Канал не найден");
        }

        var currentUserRole = await _channelUserRepository.GetUserRoleInChannelAsync(channelId, currentUserId);
        if (currentUserRole == null)
        {
            throw new InvalidOperationException("Вы не являетесь участником этого канала");
        }

        // Получаем текущего пользователя для проверки его системной роли
        var currentUser = await _userRepository.GetByIdAsync(currentUserId);
        if (currentUser == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }

        // Получаем пользователей с пагинацией
        var pagedUsers = await _userRepository
            .GetAvailableUsersForChannelAsync(channelId, currentUserId, filter);
        
        var availableUsers = new List<AvailableUserDto>();
        
        foreach (var user in pagedUsers.Items)
        {
            var dto = _mapper.Map<AvailableUserDto>(user);
            dto.AvatarUrl = await _avatarService.GetAvatarUrl(user.AvatarPresetKey);
            
            availableUsers.Add(dto);
        }

        return new PagedResult<AvailableUserDto>
        {
            Items = availableUsers,
            TotalCount = pagedUsers.TotalCount,
            Page = pagedUsers.Page,
            PageSize = pagedUsers.PageSize
        };
    }
}