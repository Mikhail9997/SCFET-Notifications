using Application.DTOs;
using Application.Hubs;
using Application.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public interface IChannelNotificationService
{
    Task SendChannelInvitationNotificationsAsync(ChannelInvitationDto invitation);
    Task SendInvitationAcceptedNotificationAsync(ChannelInvitationDto invitation);
    Task SendInvitationDeclinedNotificationAsync(ChannelInvitationDto invitation);
    Task SendInvitationCancelledNotificationAsync(ChannelInvitationDto invitation);
}

public class ChannelNotificationService:IChannelNotificationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChannelNotificationService> _logger;

    public ChannelNotificationService(IServiceProvider serviceProvider, ILogger<ChannelNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task SendChannelInvitationNotificationsAsync(ChannelInvitationDto invitation)
    {  
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            try
            {
                await hubContext.Clients.Group($"user_{invitation.InviteeId}")
                    .SendAsync("ChannelInvitation", invitation);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send channel invitation to user {UserId}", invitation.InviteeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }

    public async Task SendInvitationAcceptedNotificationAsync(ChannelInvitationDto invitation)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            try
            {
                await hubContext.Clients.Group($"user_{invitation.InviterId}")
                    .SendAsync("InvitationAccepted", invitation);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send invitation accepted to user {UserId}", invitation.InviterId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }

    public async Task SendInvitationDeclinedNotificationAsync(ChannelInvitationDto invitation)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            try
            {
                await hubContext.Clients.Group($"user_{invitation.InviterId}")
                    .SendAsync("InvitationDeclined", invitation);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send invitation declined to user {UserId}", invitation.InviterId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }

    public async Task SendInvitationCancelledNotificationAsync(ChannelInvitationDto invitation)
    {
        try
        {
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<NotificationHub>>();
            try
            {
                await hubContext.Clients.Group($"user_{invitation.InviteeId}")
                    .SendAsync("InvitationCancelled", invitation);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send invitation cancelled to user {UserId}", invitation.InviteeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hub context");
        }
    }
}