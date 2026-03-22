using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Group)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<IReadOnlyList<User>> GetUsersByRoleAsync(UserRole role)
    {
        return await _context.Users
            .Where(u => u.Role == role)
            .Include(u => u.Group)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<User>> GetUsersByGroupAsync(Guid groupId)
    {
        return await _context.Users
            .Where(u => u.GroupId == groupId)
            .Include(u => u.Group)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<User>> FilterAsync(FilterEntity query, IEnumerable<User>? users = null)
    {
        var source = users?.AsQueryable() ?? _context.Users;
    
        var filtered = ApplyFilters(source, query);
        
        if (filtered is IAsyncEnumerable<User>)
        {
            return await filtered.ToListAsync();
        }
        return filtered.ToList();
    }

    private IQueryable<User> ApplyFilters(IQueryable<User> source, FilterEntity query)
    {
        if (!string.IsNullOrEmpty(query.FirstName))
        {
            source = source.Where(user => user.FirstName.Contains(query.FirstName));
        }
    
        if (!string.IsNullOrEmpty(query.LastName))
        {
            source = source.Where(user => user.LastName.Contains(query.LastName));
        }
    
        if (!string.IsNullOrEmpty(query.Email))
        {
            source = source.Where(user => user.Email.Contains(query.Email));
        }

        if (!string.IsNullOrEmpty(query.PhoneNumber))
        {
            source = source.Where(user => user.PhoneNumber.Contains(query.PhoneNumber));
        }
        
        if (query.IsActive != null)
        {
            source = source.Where(user => user.IsActive == query.IsActive);
        }
        
        if (query.GroupId != null)
        {
            source = source.Where(user => user.GroupId == query.GroupId.Value);
        }

        return source;
    }
    
    public async Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null)
    {
        return !await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower() && 
                           (excludeUserId == null || u.Id != excludeUserId));
    }

    public async Task<bool> IsPhoneUniqueAsync(string phone)
    {
        return !await _context.Users.AnyAsync(u => u.PhoneNumber == phone);
    }

    public async Task<bool> IsTelegramIdUniqueAsync(string token)
    {
        return !await _context.Users
            .Where(u => u.Role == UserRole.Student || u.Role == UserRole.Parent)
            .AnyAsync(u => u.TelegramId != null && u.TelegramId.ToLower() == token.ToLower());
    }
    
    public override async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.Group)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
}