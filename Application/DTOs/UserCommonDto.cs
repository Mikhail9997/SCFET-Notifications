namespace Application.DTOs;

public class UserCommonDto
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? ChatId { get; set; }
}