namespace Core.Dtos;

public class ChannelMemberFilter
{
    public string? SearchTerm { get; set; } 
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 2;
}