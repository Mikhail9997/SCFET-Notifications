namespace Application.DTOs;

public class ChannelSearchDto
{
    public string? SearchTerm { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public int? MinMembers { get; set; }
    public int? MaxMembers { get; set; }
}