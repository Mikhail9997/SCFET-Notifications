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

    public async Task<List<AvatarPreset>> GetByKeysAsync(IEnumerable<string> presetKeys)
    {
        var keys = presetKeys.ToHashSet();
        
        if (!keys.Any())
            return new List<AvatarPreset>();

        return await _context.AvatarPresets
            .Where(p => keys.Contains(p.PresetKey))
            .ToListAsync();
    }

    public async Task<bool> Exists(string presetKey)
    {
        return await _context.AvatarPresets
            .AnyAsync(p => p.PresetKey == presetKey);
    }

    public async Task RemoveByKey(string presetKey)
    {
        var preset = await _context.AvatarPresets
            .FirstOrDefaultAsync(p => p.PresetKey == presetKey);

        if (preset == null) return;

        await DeleteAsync(preset);
    }

    public override async Task<IReadOnlyList<AvatarPreset>> GetAllAsync()
    {
        return await _context.AvatarPresets
            .Where(p => p.Category != "custom")
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AvatarPreset>> GetAllByCategory(string category)
    {
        return await _context.AvatarPresets
            .Where(p => p.Category == category)
            .ToListAsync();
    }
}