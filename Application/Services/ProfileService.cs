using Core.Interfaces;

namespace Application.Services;

public class ProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly FileService _fileService;

    public ProfileService(IUserRepository userRepository, FileService fileService)
    {
        _userRepository = userRepository;
        _fileService = fileService;
    }

    public async Task UploadAvatar(Guid userId, string avatarUrl)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }

        user.AvatarUrl = avatarUrl;

        await _userRepository.UpdateAsync(user);
    }
    
    public async Task RemoveAvatar(Guid userId, string uploadsFolder)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }

        if (user.AvatarUrl == null && string.IsNullOrEmpty(user.AvatarUrl))
        {
            throw new InvalidOperationException("Аватарки уже не существует");
        }

        await _fileService.DeleteImageAsync(user.AvatarUrl, uploadsFolder);
        
        user.AvatarUrl = null;
        await _userRepository.UpdateAsync(user);
    }
}