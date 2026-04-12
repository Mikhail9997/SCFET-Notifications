namespace Core.Models;



public class UserFilterEntity
{
    public string? FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public bool? IsActive { get; set; }
}

public class GroupFilterEntity
{
    public string? Name { get; set; } = string.Empty;
}

public class FilterEntity:BaseFilterEntity
{
    public SortBy SortBy { get; set; } = SortBy.CreatedAt;
}

public enum SortBy
{
    CreatedAt,
    Title,
    Name
}