using Application.DTOs;
using Application.Extensions;
using Application.Utils;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;

namespace Application.Services;

public interface IFavoritesService
{
    Task<PagedResult<FavoriteDto>> GetMyAsync(Guid userId, FilterEntity filter);
    Task AddAsync(Guid userId, AddFavoriteDto dto);
    Task RemoveAsync(Guid userId, Guid notificationId);
}

public class FavoritesService: IFavoritesService
{
    private readonly IUserFavoriteNotificationRepository _favoriteNotificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAvatarService _avatarService;
    private readonly IConfiguration _configuration;

    public FavoritesService(IUserFavoriteNotificationRepository favoriteNotificationRepository, 
        IUserRepository userRepository, 
        IAvatarService avatarService,
        IConfiguration configuration)
    {
        _favoriteNotificationRepository = favoriteNotificationRepository;
        _userRepository = userRepository;
        _avatarService = avatarService;
        _configuration = configuration;
    }

    public async Task<PagedResult<FavoriteDto>> GetMyAsync(Guid userId, FilterEntity filter)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Пользователь не найден");

        var pagedResult = await _favoriteNotificationRepository
            .GetMyAsync(userId, filter);

        List<FavoriteDto> favorites = new List<FavoriteDto>();

        foreach (var favorite in pagedResult.Items)
        {
            var n = favorite.Notification;
            FavoriteDto favoriteDto = new FavoriteDto()
            {
                NotificationId = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                SenderName = $"{n.Sender.FirstName} {n.Sender.LastName}",
                SenderRole = n.Sender.Role.ToString(),
                SenderAvatarUrl = await _avatarService.GetAvatarUrl(n.Sender.AvatarPresetKey),
                SenderId = n.SenderId,
                IsPersonal = NotificationUtils
                    .IsPersonal(n.Receivers.Select(r => r.UserId).ToHashSet(), userId),
                IsEnable = n.Receivers.Any(f => f.UserId == userId),
                CreatedAt = n.CreatedAt,
                AllowReplies = n.AllowReplies,
                IsRead = n.Receivers.FirstOrDefault(r => r.UserId == userId)?.IsRead ?? false,
                ImageUrl = !string.IsNullOrEmpty(n.ImageUrl) ? $"{_configuration["BaseUrl"]}{n.ImageUrl}" : null
            };
            favorites.Add(favoriteDto);
        }

        var result = new PagedResult<FavoriteDto>()
        {
            Items = favorites,
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount
        };
        return result;
    }

    public async Task AddAsync(Guid userId, AddFavoriteDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Пользователь не найден");

        UserFavoriteNotification userFavoriteNotification = new UserFavoriteNotification()
        {
            UserId = userId,
            NotificationId = dto.NotificationId
        };
        await _favoriteNotificationRepository.AddAsync(userFavoriteNotification);
    }

    public async Task RemoveAsync(Guid userId, Guid notificationId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Пользователь не найден");
        
        var favoriteToRemove = await _favoriteNotificationRepository.GetAsync(userId, notificationId);
        if (favoriteToRemove == null)
        {
            throw new InvalidOperationException("Не удалось найти объявление");
        }

        await _favoriteNotificationRepository.DeleteAsync(favoriteToRemove);
    }
    
}