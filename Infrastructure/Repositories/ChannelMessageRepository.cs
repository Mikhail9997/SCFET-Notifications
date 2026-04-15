using Core.Dtos;
using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ChannelMessageRepository: BaseRepository<ChannelMessage>, IChannelMessageRepository
{
    public ChannelMessageRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<ChannelMessage>> GetChannelMessagesAsync(Guid channelId, MessageFilterDto filter)
    {
        var query = _context.ChannelMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .ThenInclude(r => r!.Sender)
            .Where(m => m.ChannelId == channelId)
            .AsSplitQuery();

        query = ApplyFilters(query, filter);
        
        var totalCount = await query.CountAsync();
        
        query = ApplySorting(query, filter);
        
        var messages = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<ChannelMessage>
        {
            Items = messages,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }


    public async Task<ChannelMessage?> GetMessageWithDetailsAsync(Guid messageId)
    {
        return await _context.ChannelMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .ThenInclude(r => r!.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId);
    }

    public async Task<int> GetUnreadCountAsync(Guid channelId, Guid userId)
    {
        return await _context.ChannelMessages
            .Where(m => m.ChannelId == channelId)
            .Where(m => m.SenderId != userId)
            .Where(m => !m.IsRead)
            .CountAsync();
    }

    public async Task MarkAsReadAsync(Guid messageId, Guid userId)
    {
        var message = await _context.ChannelMessages.FindAsync(messageId);
        
        if (message != null && message.SenderId != userId && !message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await UpdateAsync(message);
        }
    }

    public async Task MarkAllAsReadAsync(Guid channelId, Guid userId)
    {
        var unreadMessages = await _context.ChannelMessages
            .Where(m => m.ChannelId == channelId)
            .Where(m => m.SenderId != userId)
            .Where(m => !m.IsRead)
            .ToListAsync();
        
        if (unreadMessages.Any())
        {
            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                
                await UpdateAsync(message);
            }
        }
    }

    public async Task<bool> CanUserModifyMessageAsync(Guid messageId, Guid userId)
    {
        // Получаем все необходимые данные одним запросом
        var messageData = await _context.ChannelMessages
            .Where(m => m.Id == messageId)
            .Select(m => new
            {
                m.SenderId,
                m.ChannelId,
                SenderRole = _context.ChannelUsers
                    .Where(cu => cu.ChannelId == m.ChannelId && cu.UserId == m.SenderId)
                    .Select(cu => cu.Role)
                    .FirstOrDefault(),
                CurrentUserRole = _context.ChannelUsers
                    .Where(cu => cu.ChannelId == m.ChannelId && cu.UserId == userId)
                    .Select(cu => cu.Role)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
    
        if (messageData == null)
            return false;
    
        // Отправитель всегда может удалять своё сообщение
        if (messageData.SenderId == userId)
            return true;
    
        // Владелец может всё
        if (messageData.CurrentUserRole == ChannelRole.Owner)
            return true;
    
        // Администратор не может удалять владельца и админов
        if (messageData.CurrentUserRole == ChannelRole.Admin)
            return messageData.SenderRole != ChannelRole.Owner && messageData.SenderRole != ChannelRole.Admin;
    
        // Модератор может удалять только участников
        if (messageData.CurrentUserRole == ChannelRole.Moderator)
            return messageData.SenderRole == ChannelRole.Member;
    
        return false;
    }

    public async Task<string> GetDeleteDenyReasonAsync(Guid messageId, Guid userId)
    {
        var message = await _context.ChannelMessages
            .Include(m => m.Sender)
            .Include(m => m.Channel)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        
        if (message == null)
            return "Сообщение не найдено";
        
        // Если это своё сообщение - причина не нужна (разрешено)
        if (message.SenderId == userId)
            return string.Empty;
        
        var currentUserRole = await _context.ChannelUsers
            .Where(cu => cu.ChannelId == message.ChannelId && cu.UserId == userId)
            .Select(cu => cu.Role)
            .FirstOrDefaultAsync();
        
        var senderRole = await _context.ChannelUsers
            .Where(cu => cu.ChannelId == message.ChannelId && cu.UserId == message.SenderId)
            .Select(cu => cu.Role)
            .FirstOrDefaultAsync();
        
        var senderName = message.Sender != null 
            ? $"{message.Sender.LastName} {message.Sender.FirstName}".Trim() 
            : "Пользователь";
        
        return currentUserRole switch
        {
            ChannelRole.Admin when senderRole == ChannelRole.Owner => 
                $"Администратор не может удалять сообщения владельца канала ({senderName})",
            ChannelRole.Admin when senderRole == ChannelRole.Admin => 
                $"Администратор не может удалять сообщения других администраторов ({senderName})",
            ChannelRole.Moderator when senderRole == ChannelRole.Owner => 
                $"Модератор не может удалять сообщения владельца канала ({senderName})",
            ChannelRole.Moderator when senderRole == ChannelRole.Admin => 
                $"Модератор не может удалять сообщения администраторов ({senderName})",
            ChannelRole.Moderator when senderRole == ChannelRole.Moderator => 
                $"Модератор не может удалять сообщения других модераторов ({senderName})",
            ChannelRole.Member => 
                "Участник может удалять только свои сообщения",
            _ => "У вас нет прав на удаление этого сообщения"
        };
    }

    private IQueryable<ChannelMessage> ApplyFilters(IQueryable<ChannelMessage> query, MessageFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(m => m.Content.ToLower().Contains(searchTerm));
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt <= filter.EndDate.Value);
        }

        return query;
    }
    
    private IQueryable<ChannelMessage> ApplySorting(IQueryable<ChannelMessage> query, MessageFilterDto filter)
    {
        return filter.SortOrder == SortOrder.Ascending
            ? query.OrderBy(m => m.CreatedAt)
            : query.OrderByDescending(m => m.CreatedAt);
    }

    public override async Task<ChannelMessage?> GetByIdAsync(Guid id)
    {
        return await GetMessageWithDetailsAsync(id);
    }
}