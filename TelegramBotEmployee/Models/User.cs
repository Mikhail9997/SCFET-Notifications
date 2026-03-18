namespace TelegramBotEmployee.Models;

public class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? ChatId { get; set; }
}