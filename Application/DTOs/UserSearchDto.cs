using Core.Models;

namespace Application.DTOs;

public class UserSearchDto
{
    public string? SearchTerm { get; set; }
    public UserRole? Role { get; set; }
    public Guid? ExcludeChannelId { get; set; }
    public bool ExcludeExistingMembers { get; set; } = true;
    public bool ExcludePendingInvitations { get; set; } = true;
}