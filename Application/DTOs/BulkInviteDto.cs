namespace Application.DTOs;

public class BulkInviteDto
{
    public Guid ChannelId { get; set; }
    public List<Guid> UserIds { get; set; } = new();
    public string? Message { get; set; }
}
