using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AvatarPresetRepository:BaseRepository<AvatarPreset>, IAvatarPresetRepository
{
    public AvatarPresetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> Exist(string presetKey)
    {
        return await _context.AvatarPresets.AnyAsync(p => p.PresetKey == presetKey);
    }

    public async Task<AvatarPreset?> GetByKey(string presetKey)
    {
        return await _context.AvatarPresets
            .FirstOrDefaultAsync(p => p.PresetKey == presetKey);
    }
}