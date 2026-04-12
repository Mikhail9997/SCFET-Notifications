using Application.DTOs;
using Application.Hubs;
using AutoMapper;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Application.Services;

public interface IChannelService
{
    Task<PagedResult<ChannelDto>> GetUserChannelsPaginatedAsync(Guid userId, ChannelFilterEntity filter);
    Task<PagedResult<ChannelInvitationDto>> GetUserInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter);
    Task<PagedResult<ChannelInvitationDto>> GetUserSentInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter);
    Task<PagedResult<ChannelDto>> GetAllChannelsPaginatedAsync(ChannelFilterEntity filter, Guid userId);
    Task<List<ChannelMemberDto>> GetChannelMembersAsync(Guid channelId);
    Task<ChannelMemberDto?> GetChannelMemberAsync(Guid channelId, Guid userId);
    Task<ChannelDto?> GetChannelByIdAsync(Guid channelId, Guid userId);
    
    Task<ChannelDto> CreateChannelAsync(Guid ownerId, CreateChannelDto dto);
    Task RemoveUserFromChannelAsync(Guid channelId, Guid userId, Guid removedById);
    Task UpdateUserRoleAsync(Guid channelId, Guid userId, ChannelRole newRole, Guid updatedById);
    Task LeaveChannelAsync(Guid channelId, Guid userId);
    Task InviteUsersToChannelAsync(Guid channelId, Guid inviterId, List<Guid> userIds, string? message = null);
    
    Task AcceptInvitationAsync(Guid invitationId, Guid userId);
    Task DeclineInvitationAsync(Guid invitationId, Guid userId);
    Task CancelInvitationAsync(Guid invitationId, Guid cancellerId);
}

public class ChannelService:IChannelService
{
    private readonly IChannelRepository _channelRepository;
    private readonly IChannelUserRepository _channelUserRepository;
    private readonly IChannelInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IChannelNotificationService _channelNotificationService;
    private readonly IAvatarService _avatarService;
    private readonly IHubContext<ChannelHub> _hubContext;

    public ChannelService(
        IChannelRepository channelRepository,
        IChannelUserRepository channelUserRepository,
        IChannelInvitationRepository invitationRepository,
        IUserRepository userRepository,
        IMapper mapper, 
        IChannelNotificationService channelNotificationService, 
        IAvatarService avatarService,
        IHubContext<ChannelHub> hubContext)
    {
        _channelRepository = channelRepository;
        _channelUserRepository = channelUserRepository;
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _channelNotificationService = channelNotificationService;
        _avatarService = avatarService;
        _hubContext = hubContext;
    }

    public async Task<PagedResult<ChannelDto>> GetUserChannelsPaginatedAsync(Guid userId, ChannelFilterEntity filter)
    {
        var pagedChannels = await _channelRepository.GetUserChannelsPaginatedAsync(userId, filter);

        var items = new List<ChannelDto>();
        foreach (Channel c in pagedChannels.Items)
        {
            var dto = await MapToDtoAsync(c, userId);
            dto.IsMember = true; //так как каналы изначально берутся для текущего пользователя
            
            items.Add(dto);
        }
        return new PagedResult<ChannelDto>
        {
            Items = items,
            TotalCount = pagedChannels.TotalCount,
            Page = pagedChannels.Page,
            PageSize = pagedChannels.PageSize
        };
    }

    public async Task<PagedResult<ChannelInvitationDto>> GetUserInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter)
    {
        var pagedInvitations = await _invitationRepository
            .GetUserInvitationsPaginatedAsync(userId, filter);
        
        var items = new List<ChannelInvitationDto>();
        foreach (ChannelInvitation c in pagedInvitations.Items)
        {
            await CheckChannelInvitationExpired(c);
            
            var dto = _mapper.Map<ChannelInvitationDto>(c);
            dto.InviteeAvatar = await _avatarService.GetAvatarUrl(c.Invitee.AvatarPresetKey);
            dto.InviterAvatar = await _avatarService.GetAvatarUrl(c.Inviter.AvatarPresetKey);
            
            items.Add(dto);
        }
        return new PagedResult<ChannelInvitationDto>
        {
            Items = items,
            TotalCount = pagedInvitations.TotalCount,
            Page = pagedInvitations.Page,
            PageSize = pagedInvitations.PageSize
        };
    }

    public async Task<PagedResult<ChannelInvitationDto>> GetUserSentInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter)
    {
        var pagedInvitations = await _invitationRepository
            .GetUserSentInvitationsPaginatedAsync(userId, filter);
        
        var items = new List<ChannelInvitationDto>();
        foreach (ChannelInvitation c in pagedInvitations.Items)
        {
            await CheckChannelInvitationExpired(c);
            
            var dto = _mapper.Map<ChannelInvitationDto>(c);
            dto.InviteeAvatar = await _avatarService.GetAvatarUrl(c.Invitee.AvatarPresetKey);
            dto.InviterAvatar = await _avatarService.GetAvatarUrl(c.Inviter.AvatarPresetKey);
            
            items.Add(dto);
        }
        return new PagedResult<ChannelInvitationDto>
        {
            Items = items,
            TotalCount = pagedInvitations.TotalCount,
            Page = pagedInvitations.Page,
            PageSize = pagedInvitations.PageSize
        };
    }

    public async Task<PagedResult<ChannelDto>> GetAllChannelsPaginatedAsync(ChannelFilterEntity filter, Guid userId)
    {
        var pagedChannels = await _channelRepository.GetAllChannelsPaginatedAsync(filter);
        
        var items = new List<ChannelDto>();
        foreach (Channel c in pagedChannels.Items)
        {
            var dto = _mapper.Map<ChannelDto>(c);
            dto.OwnerAvatar = await _avatarService.GetAvatarUrl(c.Owner.AvatarPresetKey);
            dto.IsMember = c.ChannelUsers.Any(cu => cu.UserId == userId);
            
            items.Add(dto);
        }
        return new PagedResult<ChannelDto>
        {
            Items = items,
            TotalCount = pagedChannels.TotalCount,
            Page = pagedChannels.Page,
            PageSize = pagedChannels.PageSize
        };
    }

    public async Task<List<ChannelMemberDto>> GetChannelMembersAsync(Guid channelId)
    {
        var members = await _channelUserRepository.GetChannelMembersAsync(channelId);
        List<ChannelMemberDto> memberDtos = new List<ChannelMemberDto>();
        
        foreach (ChannelUser m in members)
        {
            var dto = _mapper.Map<ChannelMemberDto>(m);
            dto.AvatarUrl = await _avatarService.GetAvatarUrl(m.User.AvatarPresetKey);
            
            memberDtos.Add(dto);
        }
        return memberDtos;
    }

    public async Task<ChannelMemberDto?> GetChannelMemberAsync(Guid channelId, Guid userId)
    {
        var member = await _channelUserRepository.GetByChannelAndUserAsync(channelId, userId);

        if (member == null)
        {
            throw new InvalidOperationException("Пользователь не является участником канала");
        }

        var dto = _mapper.Map<ChannelMemberDto>(member);
        dto.AvatarUrl = await _avatarService.GetAvatarUrl(member.User.AvatarPresetKey);

        return dto;
    }

    public async Task<ChannelDto?> GetChannelByIdAsync(Guid channelId, Guid userId)
    {
        var channel = await _channelRepository.GetByIdWithDetailsAsync(channelId);
        if (channel == null) return null;
        
        var dto = await MapToDtoAsync(channel, userId);

        return dto;
    }
    
    public async Task<ChannelDto> CreateChannelAsync(Guid ownerId, CreateChannelDto dto)
    {
        // Получаем информацию о создателе канала
        var owner = await _userRepository.GetByIdAsync(ownerId);
        if (owner == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }

        // Проверяем, не существует ли уже канал с таким именем
        var existingChannel = await _channelRepository.GetByNameAsync(dto.Name);
        if (existingChannel != null)
        {
            throw new InvalidOperationException($"Канал с названием '{dto.Name}' уже существует");
        }

        // Создаем канал
        var channel = new Channel
        {
            Name = dto.Name,
            Description = dto.Description,
            OwnerId = ownerId,
        };
        
        await _channelRepository.AddAsync(channel);
        
        // Добавляем владельца как участника
        var channelUser = new ChannelUser
        {
            ChannelId = channel.Id,
            UserId = ownerId,
            Role = ChannelRole.Owner,
        };
        
        await _channelUserRepository.AddAsync(channelUser);
        
        return await GetChannelByIdAsync(channel.Id, ownerId) 
            ?? throw new InvalidOperationException("Не удалось создать канал");
    }

    public async Task RemoveUserFromChannelAsync(Guid channelId, Guid userId, Guid removedById)
    {
        // Проверяем права удаляющего
        var removerRole = await _channelUserRepository.GetUserRoleInChannelAsync(channelId, removedById);
        
        // Удалять могут только администраторы и владелец
        if (removerRole == null || 
            (removerRole != ChannelRole.Owner && removerRole != ChannelRole.Admin))
        {
            throw new InvalidOperationException("У вас нет прав на удаление участников канала");
        }
        
        // Нельзя удалить владельца канала
        var channel = await _channelRepository.GetByIdAsync(channelId);
        if (channel?.OwnerId == userId)
        {
            throw new InvalidOperationException("Нельзя удалить владельца канала");
        }
        
        var membership = await _channelUserRepository.GetByChannelAndUserAsync(channelId, userId);
        
        if (membership == null)
        {
            throw new InvalidOperationException("Пользователь не является участником канала");
        }
        
        await _channelUserRepository.DeleteAsync(membership);
        
        // Получаем информацию об удаляемом пользователе
        var userToRemove = await _userRepository.GetByIdAsync(userId);
        var userToRemoveFullName = userToRemove != null ? $"{userToRemove.LastName} {userToRemove.FirstName}".Trim() : "Пользователь";
        
        // Получаем информацию об пользователе, который производит удаление
        var user = await _userRepository.GetByIdAsync(removedById);
        var userFullName = user != null ? $"{user.LastName} {user.FirstName}".Trim() : "Пользователь";
        
        // Уведомляем участников канала через Hub
        await _hubContext.Clients.Group($"channel_{channelId}")
            .SendAsync("UserLeft", new 
            { 
                userId, 
                userFullName, 
                channelId,
                message = $"{userToRemoveFullName} удален пользователем {userFullName}({removerRole})" 
            });
    }

    public async Task UpdateUserRoleAsync(Guid channelId, Guid userId, ChannelRole newRole, Guid updatedById)
    {
        // Проверяем права обновляющего
        var updaterRole = await _channelUserRepository.GetUserRoleInChannelAsync(channelId, updatedById);
        
        // Изменять права могут только владелец или администратор
        if (updaterRole == null || 
            (updaterRole != ChannelRole.Owner && updaterRole != ChannelRole.Admin))
        {
            throw new InvalidOperationException("У вас нет прав на обновление роли участникам канала");
        }
        
        var channel = await _channelRepository.GetByIdAsync(channelId);
        if (channel == null)
        {
            throw new InvalidOperationException("Канал не найден");
        }
        
        // Если назначаем нового владельца
        if (newRole == ChannelRole.Owner)
        {
            // Только текущий владелец может передать права
            if (channel.OwnerId != updatedById)
            {
                throw new InvalidOperationException("Только владелец может передать права на канал");
            }
            
            // Нельзя передать права самому себе
            if (userId == updatedById)
            {
                throw new InvalidOperationException("Вы уже являетесь владельцем канала");
            }
            
            // Проверяем, что новый владелец является участником канала
            var newOwnerMembership = await _channelUserRepository
                .GetByChannelAndUserAsync(channelId, userId);
            if (newOwnerMembership == null)
            {
                throw new InvalidOperationException("Пользователь не является участником канала");
            }
            
            // Обновляем владельца канала
            channel.OwnerId = userId;
            channel.UpdatedAt = DateTime.UtcNow;
            await _channelRepository.UpdateAsync(channel);
            
            // Назначаем нового владельца
            newOwnerMembership.Role = ChannelRole.Owner;
            newOwnerMembership.UpdatedAt = DateTime.UtcNow;
            await _channelUserRepository.UpdateAsync(newOwnerMembership);
            
            // Понижаем старого владельца до администратора
            var oldOwnerMembership = await _channelUserRepository.GetByChannelAndUserAsync(channelId, updatedById);
            if (oldOwnerMembership != null)
            {
                oldOwnerMembership.Role = ChannelRole.Admin; // Понижаем до администратора
                oldOwnerMembership.UpdatedAt = DateTime.UtcNow;
                await _channelUserRepository.UpdateAsync(oldOwnerMembership);
            }
            return;
        }
        
        // Для других ролей - проверяем, что не пытаемся изменить роль владельца
        if (channel.OwnerId == userId)
        {
            throw new InvalidOperationException("Нельзя изменить роль владельца. Используйте передачу прав.");
        }
        
        // Администратор не может изменять роли других администраторов
        if (updaterRole == ChannelRole.Admin)
        {
            var targetRole = await _channelUserRepository.GetUserRoleInChannelAsync(channelId, userId);
            if (targetRole == ChannelRole.Admin)
            {
                throw new InvalidOperationException("Администратор не может изменять роль другого администратора");
            }
        }
        
        var membership = await _channelUserRepository.GetByChannelAndUserAsync(channelId, userId);
        if (membership == null)
        {
            throw new InvalidOperationException("Пользователь не является участником канала");
        }
        
        membership.Role = newRole;
        membership.UpdatedAt = DateTime.UtcNow;
        
        await _channelUserRepository.UpdateAsync(membership);
    }

    public async Task LeaveChannelAsync(Guid channelId, Guid userId)
    {
        // Владелец не может покинуть канал, не передав права
        var channel = await _channelRepository.GetByIdAsync(channelId);

        if (channel == null)
        {
            throw new InvalidOperationException("Канал не найден");
        }
        
        // владелей не может покинуть канал кроме случая, когда владелей один в группе
        if (channel?.OwnerId == userId && channel.ChannelUsers.Count != 1)
        {
            throw new InvalidOperationException("Владелец не может покинуть канал, не передав права");
        }
        
        var membership = await _channelUserRepository.GetByChannelAndUserAsync(channelId, userId);
        
        if (membership == null)
        {
            throw new InvalidOperationException("Пользователь не является участником канала");
        }
        
        await _channelUserRepository.DeleteAsync(membership);
        
        // Если в канале не осталось ниодного участника, канал удаляется
        var membersCount = await _channelUserRepository.GetChannelMembersCountAsync(channelId);
        if (membersCount == 0)
        {
            await _channelRepository.DeleteAsync(channel);
        }
    }

    public async Task InviteUsersToChannelAsync(Guid channelId, Guid inviterId, List<Guid> userIds, string? message = null)
    {
        // Получаем информацию о канале
        var channel = await _channelRepository.GetByIdAsync(channelId);
        if (channel == null)
        {
            throw new InvalidOperationException("Канал не найден");
        }
        
        // Проверяем, имеет ли приглашающий право приглашать(может приглашать только владелец или админ канала)
        var inviterRole = await _channelUserRepository.GetUserRoleInChannelAsync(channelId, inviterId);
        if (inviterRole == null)
        {
            throw new InvalidOperationException("Вы не являетесь участником этого канала");
        }
        if (inviterRole != ChannelRole.Owner && inviterRole != ChannelRole.Admin)
        {
            throw new InvalidOperationException("У вас нет прав на добавление участников");
        }
        
        // Получаем информацию о приглашающем
        var inviter = await _userRepository.GetByIdAsync(inviterId);
        if (inviter == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }
        
        foreach (var userId in userIds.Distinct())
        {
            if (userId == inviterId) continue;
            
            // Получаем информацию о приглашаемом пользователе
            var invitee = await _userRepository.GetByIdAsync(userId);
            if (invitee == null)
            {
                continue;
            }
            
            // Студенты и Родители не могут приглашать Учителей и Администраторов
            if (!await CanInviteUserAsync(inviter, invitee))
            {
                continue;
            }
            
            // Проверяем, нет ли уже активного приглашения
            var hasPendingInvitation = await _invitationRepository.HasPendingInvitationAsync(channelId, userId);
            if (hasPendingInvitation)
            {
                continue;
            }
            
            // Проверяем, не является ли пользователь уже участником
            var isAlreadyMember = await _channelUserRepository.IsUserInChannelAsync(channelId, userId);
            if (isAlreadyMember)
            {
                continue;
            }
            
            var invitation = new ChannelInvitation
            {
                ChannelId = channelId,
                InviterId = inviterId,
                InviteeId = userId,
                Message = message,
                Status = InvitationStatus.Pending
            };
            
            await _invitationRepository.AddAsync(invitation);
            
            var invitationDto = _mapper.Map<ChannelInvitationDto>(invitation);
            invitationDto.InviterAvatar = await _avatarService.GetAvatarUrl(inviter.AvatarPresetKey);
            invitationDto.InviteeAvatar = await _avatarService.GetAvatarUrl(invitee.AvatarPresetKey);
            
            // Отправляем уведомления о приглашении
            await _channelNotificationService.SendChannelInvitationNotificationsAsync(invitationDto);
        }
    }

    public async Task AcceptInvitationAsync(Guid invitationId, Guid userId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        
        if (invitation == null)
        {
            throw new InvalidOperationException("Приглашение не найдено");
        }
        
        if (invitation.InviteeId != userId)
        {
            throw new InvalidOperationException("Это приглашение предназначено не вам");
        }
        
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Приглашение уже обработано. Текущий статус: {GetInvitationStatusText(invitation.Status)}");
        }
        
        // Проверяем, не истекло ли приглашение 
        if (await CheckChannelInvitationExpired(invitation))
        {
            throw new InvalidOperationException("Это приглашение уже истекло");
        }
        
        // Проверяем, существует ли канал
        var channel = await _channelRepository.GetByIdAsync(invitation.ChannelId);
        if (channel == null )
        {
            throw new InvalidOperationException("Канал не найден");
        }
        
        // Проверяем, не является ли пользователь уже участником
        var isAlreadyMember = await _channelUserRepository.IsUserInChannelAsync(invitation.ChannelId, userId);
        if (isAlreadyMember)
        {
            invitation.Status = InvitationStatus.Accepted;
            await _invitationRepository.UpdateAsync(invitation);
            throw new InvalidOperationException("Вы уже являетесь участником этого канала");
        }
        
        // Обновляем статус приглашения
        invitation.Status = InvitationStatus.Accepted;
        await _invitationRepository.UpdateAsync(invitation);
        
        // Добавляем пользователя в канал
        var channelUser = new ChannelUser
        {
            ChannelId = invitation.ChannelId,
            UserId = userId,
            Role = ChannelRole.Member
        };
        
        await _channelUserRepository.AddAsync(channelUser);
        
        // Отправляем уведомление отправителю приглашения
        var invitationDto = _mapper.Map<ChannelInvitationDto>(invitation);
        invitationDto.InviteeAvatar = await _avatarService.GetAvatarUrl(invitation.Invitee?.AvatarPresetKey);
        invitationDto.InviterAvatar = await _avatarService.GetAvatarUrl(invitation.Inviter?.AvatarPresetKey);
        
        await _channelNotificationService.SendInvitationAcceptedNotificationAsync(invitationDto);
    }

    public async Task DeclineInvitationAsync(Guid invitationId, Guid userId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
    
        if (invitation == null)
        {
            throw new InvalidOperationException("Приглашение не найдено");
        }
    
        if (invitation.InviteeId != userId)
        {
            throw new InvalidOperationException("Это приглашение предназначено не вам");
        }
    
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Приглашение уже обработано. Текущий статус: {GetInvitationStatusText(invitation.Status)}");
        }
    
        // Обновляем статус приглашения
        invitation.Status = InvitationStatus.Declined;
        await _invitationRepository.UpdateAsync(invitation);
    
        // Отправляем уведомление отправителю 
        var invitationDto = _mapper.Map<ChannelInvitationDto>(invitation);
        invitationDto.InviteeAvatar = await _avatarService.GetAvatarUrl(invitation.Invitee?.AvatarPresetKey);
        invitationDto.InviterAvatar = await _avatarService.GetAvatarUrl(invitation.Inviter?.AvatarPresetKey);
        
        await _channelNotificationService.SendInvitationDeclinedNotificationAsync(invitationDto);
    }

    public async Task CancelInvitationAsync(Guid invitationId, Guid cancellerId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        
        if (invitation == null)
        {
            throw new InvalidOperationException("Приглашение не найдено");
        }
        
        // Проверяем права на отмену
        var isInviter = invitation.InviterId == cancellerId;
        var isChannelAdmin = await _channelUserRepository.GetUserRoleInChannelAsync(invitation.ChannelId, cancellerId);
        
        if (!isInviter && isChannelAdmin != ChannelRole.Owner && isChannelAdmin != ChannelRole.Admin)
        {
            throw new InvalidOperationException("У вас нет прав на отмену этого приглашения");
        }
        
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Невозможно отменить приглашение. Текущий статус: {GetInvitationStatusText(invitation.Status)}");
        }
        
        // Удаляем приглашение или помечаем как отмененное
        await _invitationRepository.DeleteAsync(invitation);
        
        // Отправляем уведомление приглашенному пользователю
        var invitationDto = _mapper.Map<ChannelInvitationDto>(invitation);
        invitationDto.InviteeAvatar = await _avatarService.GetAvatarUrl(invitation.Invitee?.AvatarPresetKey);
        invitationDto.InviterAvatar = await _avatarService.GetAvatarUrl(invitation.Inviter?.AvatarPresetKey);
        
        await _channelNotificationService.SendInvitationCancelledNotificationAsync(invitationDto);
    }
    
    private string GetInvitationStatusText(InvitationStatus status)
    {
        return status switch
        {
            InvitationStatus.Pending => "Ожидает ответа",
            InvitationStatus.Accepted => "Принято",
            InvitationStatus.Declined => "Отклонено",
            InvitationStatus.Expired => "Истекло",
            _ => "Неизвестно"
        };
    }

    private async Task<bool> CanInviteUserAsync(User inviter, User invitee)
    {
        // Учителя и Администраторы могут приглашать кого угодно
        if (inviter.Role == UserRole.Teacher || inviter.Role == UserRole.Administrator)
        {
            return true;
        }
        
        // Студенты и Родители не могут приглашать Учителей и Администраторов
        if ((inviter.Role == UserRole.Student || inviter.Role == UserRole.Parent) &&
            (invitee.Role == UserRole.Teacher || invitee.Role == UserRole.Administrator))
        {
            return false;
        }
        
        // В остальных случаях можно приглашать
        return true;
    }
    
    private async Task<bool> CheckChannelInvitationExpired(ChannelInvitation invitation)
    {
        // обновляем статус заявки на expired если заявка истекла(срок заявки 7 дней)
        if (invitation.Status == InvitationStatus.Pending && invitation.CreatedAt.AddDays(7) < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
            return true;
        }

        return false;
    }
    
    private async Task<ChannelDto> MapToDtoAsync(Channel c, Guid currentUserId)
    {
        var dto = _mapper.Map<ChannelDto>(c);
        dto.OwnerAvatar = await _avatarService.GetAvatarUrl(c.Owner.AvatarPresetKey);
        dto.IsOwner = c.OwnerId == currentUserId;
        dto.UserRole = await _channelUserRepository.GetUserRoleInChannelAsync(c.Id, currentUserId);
        return dto;
    }
}