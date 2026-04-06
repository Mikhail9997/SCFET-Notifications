namespace Application.DTOs;

public class UserFilterDto
{
    public string? FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public string? Email { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
}

public class GroupFilterDto
{
    public string? Name { get; set; } = string.Empty;
}