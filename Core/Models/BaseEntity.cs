namespace Core.Models;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class BaseFilterEntity
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 2;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
}

public enum SortOrder
{
    Ascending,
    Descending
}