using Core.Dtos;
using Core.Dtos.Filters;
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

    public async Task<IReadOnlyList<User>> FilterAsync(UserFilterEntity query, IEnumerable<User>? users = null)
    {
        var source = users?.AsQueryable() ?? _context.Users;
    
        var filtered = ApplyFilters(source, query);
        
        if (filtered is IAsyncEnumerable<User>)
        {
            return await filtered.ToListAsync();
        }
        return filtered.ToList();
    }

    private IQueryable<User> ApplyFilters(IQueryable<User> source, UserFilterEntity query)
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
    
    public async Task<PagedResult<User>> GetAvailableUsersForChannelAsync(
        Guid channelId, 
        Guid currentUserId,
        AvailableUsersFilterDto filter)
    {
       // Получаем текущего пользователя для определения его роли
        var currentUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId);
        
        if (currentUser == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }

        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.Group)
            .Where(u => u.IsActive)
            .AsQueryable();

        // Применяем фильтр по ролям в зависимости от роли текущего пользователя
        query = ApplyRoleBasedFilter(query, currentUser.Role, filter);

        // Применяем остальные фильтры
        query = ApplyAvailableUsersFilters(query, filter);

        // Исключаем существующих участников канала
        var existingMemberIds = await _context.ChannelUsers
            .Where(cu => cu.ChannelId == channelId)
            .Select(cu => cu.UserId)
            .ToListAsync();
        
        if (existingMemberIds.Any())
        {
            query = query.Where(u => !existingMemberIds.Contains(u.Id));
        }

        // Исключаем пользователей с активными приглашениями
        var pendingInvitationUserIds = await _context.ChannelInvitations
            .Where(ci => ci.ChannelId == channelId && ci.Status == InvitationStatus.Pending)
            .Select(ci => ci.InviteeId)
            .ToListAsync();
        
        if (pendingInvitationUserIds.Any())
        {
            query = query.Where(u => !pendingInvitationUserIds.Contains(u.Id));
        }
        
        var totalCount = await query.CountAsync();

        // Применяем сортировку и пагинацию
        query = ApplySorting(query, filter);
        
        var users = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<User>
        {
            Items = users,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<List<User>> GetByIdsAsync(IEnumerable<Guid> userIds)
    {
        var userIdsSet = userIds.ToHashSet();
        
        if (!userIdsSet.Any())
            return new List<User>();

        return await _context.Users
            .Where(u => userIdsSet.Contains(u.Id))
            .ToListAsync();
    }

    private IQueryable<User> ApplyAvailableUsersFilters(IQueryable<User> query, AvailableUsersFilterDto filter)
    {
        // Поиск по имени, фамилии или email
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(u => 
                u.FirstName.ToLower().Contains(searchTerm) ||
                u.LastName.ToLower().Contains(searchTerm) ||
                u.Email.ToLower().Contains(searchTerm) || 
                (u.FirstName + " " + u.LastName).ToLower().Contains(searchTerm));
        }

        // Фильтр по группе
        if (filter.GroupId.HasValue)
        {
            query = query.Where(u => u.GroupId == filter.GroupId.Value);
        }

        // Фильтр по дате создания
        if (filter.StartDate.HasValue)
        {
            var startDate = new DateTime(
                filter.StartDate.Value.Year,
                filter.StartDate.Value.Month,
                filter.StartDate.Value.Day,
                0, 0, 0, DateTimeKind.Utc);
            
            query = query.Where(u => u.CreatedAt >= startDate);
        }

        if (filter.EndDate.HasValue)
        {
            var endDate = new DateTime(
                filter.EndDate.Value.Year,
                filter.EndDate.Value.Month,
                filter.EndDate.Value.Day,
                23, 59, 59, DateTimeKind.Utc);
            
            query = query.Where(u => u.CreatedAt <= endDate);
        }

        return query;
    }
    
    private IQueryable<User> ApplySorting(IQueryable<User> query, AvailableUsersFilterDto filter)
    {
        switch (filter.SortBy)
        {
            case SortBy.Title:
            case SortBy.Name:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                    : query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName);
                break;
                
            case SortBy.CreatedAt:
            default:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(u => u.CreatedAt)
                    : query.OrderByDescending(u => u.CreatedAt);
                break;
        }

        return query;
    }
    
    private IQueryable<User> ApplyRoleBasedFilter(IQueryable<User> query, UserRole currentUserRole, AvailableUsersFilterDto filter)
    {
        // Если в фильтре уже указана конкретная роль, применяем её
        if (filter.Role.HasValue)
        {
            // Проверяем, может ли текущий пользователь видеть пользователей с запрошенной ролью
            if (!CanViewRole(currentUserRole, filter.Role.Value))
            {
                // Если не может - возвращаем пустой запрос
                return query.Where(u => false);
            }
        
            return query.Where(u => u.Role == filter.Role.Value);
        }

        // Если роль не указана, применяем ограничения в зависимости от роли текущего пользователя
        return currentUserRole switch
        {
            UserRole.Student => query.Where(u => u.Role == UserRole.Student || u.Role == UserRole.Parent),
            UserRole.Parent => query.Where(u => u.Role == UserRole.Student || u.Role == UserRole.Parent),
            UserRole.Teacher => query, // Учителя видят всех
            UserRole.Administrator => query, // Админы видят всех
            _ => query
        };
    }
    
    // Проверка, может ли пользователь с указанной ролью видеть пользователей с целевой ролью
    private bool CanViewRole(UserRole viewerRole, UserRole targetRole)
    {
        return viewerRole switch
        {
            UserRole.Student => targetRole == UserRole.Student || targetRole == UserRole.Parent,
            UserRole.Parent => targetRole == UserRole.Student || targetRole == UserRole.Parent,
            UserRole.Teacher => true, // Учителя видят все роли
            UserRole.Administrator => true, // Админы видят все роли
            _ => false
        };
    }
}