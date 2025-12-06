namespace Application.DTOs;

public class NotificationReceiverDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}