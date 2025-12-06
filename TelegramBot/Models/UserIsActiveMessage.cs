namespace TelegramBot.Models;

public class UserIsActiveMessage
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid UserId { get; set; }
}