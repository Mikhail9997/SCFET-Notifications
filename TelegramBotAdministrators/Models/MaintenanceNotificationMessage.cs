namespace TelegramBotAdministrators.Models;

public class MaintenanceNotificationMessage
{
    public string Title { get; set; } = "Технические работы";
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}