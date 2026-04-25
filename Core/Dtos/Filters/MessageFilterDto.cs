using Core.Models;

namespace Core.Dtos.Filters;

public class MessageFilterDto : BaseFilterEntity
{
    public string? SearchTerm { get; set; }
}