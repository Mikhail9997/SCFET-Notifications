using Core.Models;

namespace Application.DTOs;

public class GroupDto:BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}