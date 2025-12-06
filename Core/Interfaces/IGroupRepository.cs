using Core.Models;

namespace Core.Interfaces;

public interface IGroupRepository : IRepository<Group>
{
    Task<Group?> GetByNameAsync(string name);
    Task<List<Group>> FilterAsync(string name);
}