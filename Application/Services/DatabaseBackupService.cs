using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Services;

public class DatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(IConfiguration configuration, ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> CreateBackupAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);

        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"backup_{timestamp}.sql");
        
        string pgDumpPath = "pg_dump";
        
        var arguments = $"-h {connectionStringBuilder.Host} " +
                        $"-p {connectionStringBuilder.Port} " +
                        $"-U {connectionStringBuilder.Username} " +
                        $"-d {connectionStringBuilder.Database} " +
                        $"-F c " + 
                        $"-f \"{backupPath}\"";
        
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    EnvironmentVariables = { ["PGPASSWORD"] = connectionStringBuilder.Password }
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                _logger.LogInformation($"Backup created successfully: {backupPath}");
                return backupPath;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Backup failed: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup creation failed");
            throw;
        }
    }
}