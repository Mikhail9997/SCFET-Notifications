namespace TelegramBot.Models;

public class BotUserState
{
    public long ChatId { get; set; }
    public RegistrationState State { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Password { get; set; }
    public string? SelectedGroup { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}

public enum RegistrationState
{
    Start,
    WaitingForEmail,
    WaitingForFirstName,
    WaitingForLastName,
    WaitingForPassword,
    WaitingForGroupSelection,
    Completed
}