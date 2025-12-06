using System.Security.Claims;
using Application.Common.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Http;

namespace Application.Common.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userId = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
            return Guid.TryParse(userId, out var result) ? result : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?
        .FindFirst(ClaimTypes.Email)?.Value;

    public UserRole? Role
    {
        get
        {
            var role = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ClaimTypes.Role)?.Value;
                
            return Enum.TryParse<UserRole>(role, out var result) ? result : null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;
}