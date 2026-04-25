using Core.Dtos;
using Core.Models;

namespace Core.Interfaces;

public interface IChannelMessageRepository : IRepository<ChannelMessage>
{
    Task<PagedResult<ChannelMessage>> GetChannelMessagesAsync(Guid channelId, MessageFilterDto filter);
    Task<ChannelMessage?> GetMessageWithDetailsAsync(Guid messageId);
    Task<int> GetUnreadCountAsync(Guid channelId, Guid userId);
    Task MarkAsReadAsync(Guid messageId, Guid userId);
    Task MarkAllAsReadAsync(Guid channelId, Guid userId);
    Task ClearReplyReferencesAsync(Guid messageId);
    Task<int> MarkMessagesAsReadAsync(Guid channelId, HashSet<Guid> messageIds, Guid userId);
    Task<bool> CanUserModifyMessageAsync(Guid messageId, Guid userId);
    Task<string> GetDeleteDenyReasonAsync(Guid messageId, Guid userId);
}