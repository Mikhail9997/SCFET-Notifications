using Core.Models;

namespace Application.DTOs;

public class NotificationFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 2;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public NotificationSortOrder SortOrder { get; set; } = NotificationSortOrder.Descending;
    public NotificationSortBy SortBy { get; set; } = NotificationSortBy.CreatedAt;
}