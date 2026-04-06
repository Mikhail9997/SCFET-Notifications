using Core.Models;

namespace Application.DTOs;

public class FilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 2;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
    public SortBy SortBy { get; set; } = SortBy.CreatedAt;
}