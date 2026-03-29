using Core.Models;

namespace Core.Interfaces;

public interface IAvatarPresetRepository:IRepository<AvatarPreset>
{
    Task<bool> Exist(string presetKey);
    Task<AvatarPreset?> GetByKey(string presetKey);
}