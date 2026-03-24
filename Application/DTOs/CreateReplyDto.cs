namespace Application.DTOs;

public class CreateReplyDto
{
    public Guid NotificationId { get; set; }
    public string Message { get; set; } = string.Empty;
}