namespace Application.DTOs;

public class FilterDto
{
    public string? FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public string? Email { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
}

public class GroupFilterDto
{
    public string? Name { get; set; } = string.Empty;
}