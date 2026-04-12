using Core.Models;

namespace Core.Dtos;

public class AvailableUsersFilterDto : FilterEntity
{
    public UserRole? Role { get; set; }
    public Guid? GroupId { get; set; }
    public string? SearchTerm { get; set; } 
}