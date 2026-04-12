using System.Security.Claims;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Application.Hubs;

[Authorize]
public class ChannelHub:Hub
{
    private readonly IChannelUserRepository _channelUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ChannelHub> _logger;

    public ChannelHub(
        IChannelUserRepository channelUserRepository,
        IUserRepository userRepository,
        ILogger<ChannelHub> logger)
    {
        _channelUserRepository = channelUserRepository;
        _userRepository = userRepository;
        _logger = logger;
    }
    
    public async Task JoinChannel(Guid channelId)
    {
        try
        {
            var userId = GetUserId();
            
            // Проверяем, является ли пользователь участником канала
            var isMember = await _channelUserRepository.IsUserInChannelAsync(channelId, userId);
            if (!isMember)
            {
                _logger.LogWarning("User {UserId} tried to join channel {ChannelId} but is not a member", userId, channelId);
                throw new HubException("Вы не являетесь участником этого канала");
            }

            var user = await _userRepository.GetByIdAsync(userId);
            var userFullName = user != null ? $"{user.LastName} {user.FirstName}".Trim() : "Пользователь";

            await Groups.AddToGroupAsync(Context.ConnectionId, $"channel_{channelId}");
            
            await Clients.Group($"channel_{channelId}")
                .SendAsync("UserJoined", new 
                { 
                    userId, 
                    userFullName, 
                    channelId,
                    message = $"{userFullName} присоединился к каналу" 
                });

            _logger.LogInformation("User {UserId} joined channel {ChannelId}", userId, channelId);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining channel {ChannelId}", channelId);
            throw new HubException("Произошла ошибка при входе в канал");
        }
    }
    
    public async Task LeaveChannel(Guid channelId)
    {
        try
        {
            var userId = GetUserId();
            var user = await _userRepository.GetByIdAsync(userId);
            var userFullName = user != null ? $"{user.LastName} {user.FirstName}".Trim() : "Пользователь";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel_{channelId}");
            
            await Clients.Group($"channel_{channelId}")
                .SendAsync("UserLeft", new 
                { 
                    userId, 
                    userFullName, 
                    channelId,
                    message = $"{userFullName} покинул канал" 
                });

            _logger.LogInformation("User {UserId} left channel {ChannelId}", userId, channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving channel {ChannelId}", channelId);
            throw new HubException("Произошла ошибка при выходе из канала");
        }
    }
    
    public async Task Typing(Guid channelId, bool isTyping)
    {
        try
        {
            var userId = GetUserId();
            var user = await _userRepository.GetByIdAsync(userId);
            var userFullName = user != null ? $"{user.LastName} {user.FirstName}".Trim() : "Пользователь";

            await Clients.OthersInGroup($"channel_{channelId}")
                .SendAsync("UserTyping", new { userId, userFullName, channelId, isTyping });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending typing status for channel {ChannelId}", channelId);
        }
    }
    
    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            throw new HubException("Пользователь не аутентифицирован");
        }
        
        return Guid.Parse(userIdClaim);
    }
}