using Microsoft.AspNetCore.Http;

namespace Application.DTOs;

public class SendMessageRequestDto
{
    public string Content { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
    public IFormFile? Image { get; set; }
}