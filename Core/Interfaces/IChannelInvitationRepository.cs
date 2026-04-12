using Core.Models;

namespace Core.Interfaces;

public interface IChannelInvitationRepository : IRepository<ChannelInvitation>
{
    Task<List<ChannelInvitation>> GetPendingInvitationsForUserAsync(Guid userId);
    Task<PagedResult<ChannelInvitation>> GetUserInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter);
    Task<PagedResult<ChannelInvitation>> GetUserSentInvitationsPaginatedAsync(Guid userId, ChannelFilterEntity filter);
    Task<List<ChannelInvitation>> GetInvitationsForChannelAsync(Guid channelId);
    Task<ChannelInvitation?> GetInvitationWithDetailsAsync(Guid invitationId);
    Task<List<ChannelInvitation>> GetUserSentInvitationsAsync(Guid userId);
    Task<bool> HasPendingInvitationAsync(Guid channelId, Guid userId);
    Task<ChannelInvitation?> GetPendingInvitationAsync(Guid channelId, Guid userId);
}