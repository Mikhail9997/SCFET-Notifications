namespace Core.Models;

public class FilterEntity
{
    public string? FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public bool? IsActive { get; set; }
}

public class GroupFilterEntity
{
    public string? Name { get; set; } = string.Empty;
}

public class NotificationFilterEntity
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 2;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public NotificationSortOrder SortOrder { get; set; } = NotificationSortOrder.Descending;
    public NotificationSortBy SortBy { get; set; } = NotificationSortBy.CreatedAt;
}

public enum NotificationSortOrder
{
    Ascending,
    Descending
}
public enum NotificationSortBy
{
    CreatedAt,
    Title
}