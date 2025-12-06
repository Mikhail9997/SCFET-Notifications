namespace TelegramBotAdministrators.Models;

public class GroupState
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public GroupCreationState? State { get; set; }
}

public enum GroupCreationState
{
    WaitingForName,
    WaitingForDescription,
    Completed
}