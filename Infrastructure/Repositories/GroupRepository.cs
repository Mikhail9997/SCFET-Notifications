using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class GroupRepository : BaseRepository<Group>, IGroupRepository
{
    public GroupRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Group?> GetByNameAsync(string name)
    {
        return await _context.Groups
            .FirstOrDefaultAsync(g => g.Name.ToLower() == name.ToLower());
    }

    public async Task<List<Group>> FilterAsync(string name)
    {
        return await _context.Groups
            .Include(g => g.Students)
            .Where(g => g.Name.Contains(name))
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public override async Task<Group?> GetByIdAsync(Guid id)
    {
        return await _context.Groups
            .Include(g => g.Students)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public override async Task<IReadOnlyList<Group>> GetAllAsync()
    {
        return await _context.Groups
            .Include(g => g.Students)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }
}