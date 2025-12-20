using Application.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class ScheduledBackupService:BackgroundService
{
    private BackupSettings backupSettings { get; set; }
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledBackupService> _logger;
    
    public ScheduledBackupService(IOptions<BackupSettings> backupOpts, ILogger<ScheduledBackupService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        backupSettings = backupOpts.Value;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backupInterval = backupSettings.IntervalHours;
        var maxBackups = backupSettings.MaxBackups;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var backupService = scope.ServiceProvider.GetRequiredService<DatabaseBackupService>();
                    
                    await backupService.CreateBackupAsync();
                    CleanupOldBackups(maxBackups);
                }
                
                _logger.LogInformation($"Next backup in {backupInterval} hours");
                await Task.Delay(TimeSpan.FromHours(backupInterval), stoppingToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private void CleanupOldBackups(int maxBackups)
    {
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
            return;

        var backups = Directory.GetFiles(backupDir)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        if (backups.Count > 0)
        {
            foreach (var oldBackup in backups.Skip(maxBackups))
            {
                oldBackup.Delete();
                _logger.LogInformation($"Deleted old backup: {oldBackup.Name}");
            }
        }
    }
}