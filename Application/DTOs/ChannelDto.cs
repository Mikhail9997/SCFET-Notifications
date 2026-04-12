using Core.Models;

namespace Application.DTOs;

public class ChannelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerAvatar { get; set; }
    public int MembersCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ChannelRole? UserRole { get; set; } // Роль текущего пользователя в канале
    public bool IsOwner { get; set; }
    public bool IsMember { get; set; }
}