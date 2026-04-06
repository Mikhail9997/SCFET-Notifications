using Core.Models;

namespace Application.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string SenderAvatarUrl  { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public bool IsPersonal { get; set; }
    public bool IsFavorite { get; set; }
    public bool AllowReplies { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? ImageUrl { get; set; } = string.Empty;
}

public class GetItemsDto<T>
{
    public IReadOnlyList<T> Items { get; set; } 
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}