namespace Application.Events;

public class UserIsActiveEvent
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string BotId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}