namespace TelegramBotAdministrators.Models;

public class BotUserState
{
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public string? AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; } = string.Empty;
    public LoginState State { get; set; }
    public GroupState? GroupState { get; set; }
    public bool IsRefreshing { get; set; }
}

public enum LoginState
{
    WaitingForEmail,
    WaitingForPassword,
    Completed
}