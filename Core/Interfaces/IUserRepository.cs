using System.Linq.Expressions;
using Core.Dtos;
using Core.Models;

namespace Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<IReadOnlyList<User>> GetUsersByRoleAsync(UserRole role);
    Task<IReadOnlyList<User>> GetUsersByGroupAsync(Guid groupId);
    Task<IReadOnlyList<User>> FilterAsync(UserFilterEntity query, IEnumerable<User>? users = null);
    Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null);
    Task<bool> IsPhoneUniqueAsync(string phone);
    Task<bool> IsTelegramIdUniqueAsync(string token);
    Task<PagedResult<User>> GetAvailableUsersForChannelAsync(
        Guid channelId, 
        Guid currentUserId,
        AvailableUsersFilterDto filter);
}