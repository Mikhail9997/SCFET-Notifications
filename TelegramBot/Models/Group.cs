namespace TelegramBot.Models;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}