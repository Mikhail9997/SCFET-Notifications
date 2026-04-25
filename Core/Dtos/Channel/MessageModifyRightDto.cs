namespace Core.Dtos.Channel;

public class MessageModifyRightDto
{
    public Guid MessageId { get; set; }
    public bool CanDelete { get; set; }
}