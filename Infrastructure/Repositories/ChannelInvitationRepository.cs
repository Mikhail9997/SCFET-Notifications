using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ChannelInvitationRepository : BaseRepository<ChannelInvitation>, IChannelInvitationRepository
{
    public ChannelInvitationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<ChannelInvitation>> GetPendingInvitationsForUserAsync(Guid userId)
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Channel)
            .Include(ci => ci.Inviter)
            .Where(ci => ci.InviteeId == userId && ci.Status == InvitationStatus.Pending)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ChannelInvitation>> GetInvitationsForChannelAsync(Guid channelId)
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Invitee)
            .Include(ci => ci.Inviter)
            .Where(ci => ci.ChannelId == channelId)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync();
    }

    public async Task<ChannelInvitation?> GetInvitationWithDetailsAsync(Guid invitationId)
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Channel)
            .Include(ci => ci.Inviter)
            .Include(ci => ci.Invitee)
            .FirstOrDefaultAsync(ci => ci.Id == invitationId);
    }

    public async Task<List<ChannelInvitation>> GetUserSentInvitationsAsync(Guid userId)
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Channel)
            .Include(ci => ci.Invitee)
            .Where(ci => ci.InviterId == userId)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> HasPendingInvitationAsync(Guid channelId, Guid userId)
    {
        return await _context.ChannelInvitations
            .AnyAsync(ci => ci.ChannelId == channelId && 
                           ci.InviteeId == userId && 
                           ci.Status == InvitationStatus.Pending);
    }

    public async Task<ChannelInvitation?> GetPendingInvitationAsync(Guid channelId, Guid userId)
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Channel)
            .Include(ci => ci.Inviter)
            .FirstOrDefaultAsync(ci => ci.ChannelId == channelId && 
                                       ci.InviteeId == userId && 
                                       ci.Status == InvitationStatus.Pending);
    }
    
    public async Task<PagedResult<ChannelInvitation>> GetUserInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter)
    {
        var query = _context.ChannelInvitations
            .AsNoTracking()
            .Include(ci => ci.Channel)
            .Include(ci => ci.Inviter)
            .Include(ci => ci.Invitee)
            .Where(ci => ci.InviteeId == userId)
            .AsSplitQuery();

        (query, int totalCount) = await ApplyInvitationFiltersAsync(query, filter);
        
        var invitations = await query.ToListAsync();

        return new PagedResult<ChannelInvitation>
        {
            Items = invitations,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PagedResult<ChannelInvitation>> GetUserSentInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter)
    {
        var query = _context.ChannelInvitations
            .AsNoTracking()
            .Include(ci => ci.Channel)
            .Include(ci => ci.Invitee)
            .Include(ci => ci.Inviter)
            .Where(ci => ci.InviterId == userId)
            .AsSplitQuery();

        (query, int totalCount) = await ApplyInvitationFiltersAsync(query, filter);
        
        var invitations = await query.ToListAsync();

        return new PagedResult<ChannelInvitation>
        {
            Items = invitations,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
    
    private async Task<(IQueryable<ChannelInvitation> invitations, int totalCount)> ApplyInvitationFiltersAsync(
            IQueryable<ChannelInvitation> query, ChannelFilterEntity filter)
    {
        // Поиск по названию канала или сообщению
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(ci => 
                ci.Channel.Name.ToLower().Contains(searchTerm) || 
                (ci.Message != null && ci.Message.ToLower().Contains(searchTerm)));
        }

        // Фильтр по дате создания
        if (filter.StartDate.HasValue)
        {
            var startDate = new DateTime(
                filter.StartDate.Value.Year,
                filter.StartDate.Value.Month,
                filter.StartDate.Value.Day,
                0, 0, 0, DateTimeKind.Utc);
        
            query = query.Where(ci => ci.CreatedAt >= startDate);
        }

        if (filter.EndDate.HasValue)
        {
            var endDate = new DateTime(
                filter.EndDate.Value.Year,
                filter.EndDate.Value.Month,
                filter.EndDate.Value.Day,
                23, 59, 59, DateTimeKind.Utc);
        
            query = query.Where(ci => ci.CreatedAt <= endDate);
        }

        var totalCount = await query.CountAsync();
        
        // Сортировка
        switch (filter.SortBy)
        {
            case ChannelSortBy.Status:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(ci => ci.Status)
                    : query.OrderByDescending(ci => ci.Status);
                break;
                
            case ChannelSortBy.Title:
            case ChannelSortBy.Name:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(ci => ci.Channel.Name)
                    : query.OrderByDescending(ci => ci.Channel.Name);
                break;
                
            case ChannelSortBy.CreatedAt:
            default:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(ci => ci.CreatedAt)
                    : query.OrderByDescending(ci => ci.CreatedAt);
                break;
        }

        var invitations = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return (invitations, totalCount);
    }
    
    public override async Task<ChannelInvitation?> GetByIdAsync(Guid id)
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Channel)
            .Include(ci => ci.Inviter)
            .Include(ci => ci.Invitee)
            .FirstOrDefaultAsync(ci => ci.Id == id);
    }

    public override async Task<IReadOnlyList<ChannelInvitation>> GetAllAsync()
    {
        return await _context.ChannelInvitations
            .Include(ci => ci.Channel)
            .Include(ci => ci.Inviter)
            .Include(ci => ci.Invitee)
            .OrderByDescending(ci => ci.CreatedAt)
            .ToListAsync();
    }
}