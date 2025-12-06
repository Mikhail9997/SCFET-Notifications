namespace TelegramBotEmployee.Models;

public class BotUserState
{
    public long ChatId { get; set; }
    public RegistrationState State { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Password { get; set; }
    public UserRole Role { get; set; }
    public int AccountsCount { get; set; } = 0; //количество созданных аккаунтов за день
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public DateTime LastAccountCreationDate { get; set; } = DateTime.UtcNow; // Добавляем дату последнего создания аккаунта
}

public enum RegistrationState
{
    Start,
    WaitingForEmail,
    WaitingForFirstName,
    WaitingForLastName,
    WaitingForPassword,
    Completed,
    Expired
}