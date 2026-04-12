using Core.Models;

namespace Core.Dtos;

public class MessageFilterDto : BaseFilterEntity
{
    public string? SearchTerm { get; set; }
}