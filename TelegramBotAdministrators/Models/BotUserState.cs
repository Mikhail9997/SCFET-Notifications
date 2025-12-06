namespace TelegramBotAdministrators.Models;

public class BotUserState
{
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public string? Token { get; set; } = string.Empty;
    public LoginState State { get; set; }
    public GroupState? GroupState { get; set; }
}

public enum LoginState
{
    WaitingForEmail,
    WaitingForPassword,
    Completed
}