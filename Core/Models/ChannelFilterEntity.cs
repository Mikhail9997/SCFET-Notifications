namespace Core.Models;

public class ChannelFilterEntity:BaseFilterEntity
{
    public string? SearchTerm { get; set; } 
    public ChannelSortBy SortBy { get; set; } = ChannelSortBy.CreatedAt;
}

public enum ChannelSortBy
{
    CreatedAt,
    Title,
    Name,
    MembersCount,
    Status
}