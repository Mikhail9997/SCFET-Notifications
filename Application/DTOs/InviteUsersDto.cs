namespace Application.DTOs;

public class InviteUsersDto
{
    public List<Guid> UserIds { get; set; } = new();
    public string? Message { get; set; }
}