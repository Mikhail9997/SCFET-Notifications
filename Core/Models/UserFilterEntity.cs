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

public class FilterEntity
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 2;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
    public SortBy SortBy { get; set; } = SortBy.CreatedAt;
}

public enum SortOrder
{
    Ascending,
    Descending
}
public enum SortBy
{
    CreatedAt,
    Title
}