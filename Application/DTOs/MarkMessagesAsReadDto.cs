namespace Application.DTOs;

public class MarkMessagesAsReadDto
{
    public List<Guid> MessageIds { get; set; } = new();
}