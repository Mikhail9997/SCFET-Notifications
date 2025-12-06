using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Application.Hubs;

    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }
        
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                // Добавляем пользователя в группу по его ID для таргетированной отправки
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                // Также добавляем в группу по роли для массовых рассылок
                if (!string.IsNullOrEmpty(userRole))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{userRole}");
                }
                
                _logger.LogInformation("User {UserId} with role {UserRole} connected to NotificationHub", userId, userRole);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                if (!string.IsNullOrEmpty(userRole))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"role_{userRole}");
                }
                
                _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Метод для отметки уведомления как прочитанного
        public async Task MarkAsRead(Guid notificationId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
            {
                // Здесь можно вызвать сервис для отметки прочтения
                _logger.LogInformation("User {UserId} marked notification {NotificationId} as read", userId, notificationId);
                
                // Уведомляем других клиентов этого пользователя (если подключено несколько устройств)
                await Clients.Group($"user_{userId}").SendAsync("NotificationRead", notificationId);
            }
        }
    }