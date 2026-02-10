namespace Core.Models;

public class Group : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    // Навигация
    public ICollection<User> Students { get; set; } = new List<User>();
}