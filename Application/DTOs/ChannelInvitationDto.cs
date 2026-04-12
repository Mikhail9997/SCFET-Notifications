using Core.Models;

namespace Application.DTOs;

public class ChannelInvitationDto
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string? ChannelDescription { get; set; }
    public Guid InviterId { get; set; }
    public string InviterName { get; set; } = string.Empty;
    public string? InviterAvatar { get; set; }
    public Guid InviteeId { get; set; }
    public string InviteeName { get; set; } = string.Empty;
    public string? InviteeAvatar { get; set; }
    public string? Message { get; set; }
    public InvitationStatus Status { get; set; }
    public string StatusText => GetStatusText();
    public string StatusColor => GetStatusColor();
    public DateTime CreatedAt { get; set; }
    public bool IsExpired => Status == InvitationStatus.Pending && CreatedAt.AddDays(7) < DateTime.UtcNow;
    public string? TimeAgo => GetTimeAgo();
    
    private string GetStatusText()
    {
        return Status switch
        {
            InvitationStatus.Pending => "Ожидает ответа",
            InvitationStatus.Accepted => "Принято",
            InvitationStatus.Declined => "Отклонено",
            InvitationStatus.Expired => "Истекло",
            _ => "Неизвестно"
        };
    }
    
    private string GetStatusColor()
    {
        return Status switch
        {
            InvitationStatus.Pending => "#FFA500", // Orange
            InvitationStatus.Accepted => "#4CAF50", // Green
            InvitationStatus.Declined => "#F44336", // Red
            InvitationStatus.Expired => "#9E9E9E", // Gray
            _ => "#000000"
        };
    }
    
    private string GetTimeAgo()
    {
        var timeSpan = DateTime.UtcNow - CreatedAt;
        
        if (timeSpan.TotalDays > 365)
            return $"{(int)(timeSpan.TotalDays / 365)} г. назад";
        if (timeSpan.TotalDays > 30)
            return $"{(int)(timeSpan.TotalDays / 30)} мес. назад";
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays} д. назад";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours} ч. назад";
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes} мин. назад";
        
        return "Только что";
    }
}