using Core.Interfaces;

namespace Application.Services;

public class ProfileService
{
    private readonly IUserRepository _userRepository;

    public ProfileService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task UpdateAvatarPreset(Guid userId, string presetKey)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Пользователь не найден");
        
        user.AvatarPresetKey = presetKey;
        await _userRepository.UpdateAsync(user);
    }
}