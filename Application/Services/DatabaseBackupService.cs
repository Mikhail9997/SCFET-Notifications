using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.AspNetCore.Hosting;

namespace Application.Services;

public interface IDatabaseBackupService
{
    Task<string> CreateBackupAsync();
}

public class DatabaseBackupService:IDatabaseBackupService
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

public class DatabaseBackupDockerService : IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly DockerClient _dockerClient;
    
    public DatabaseBackupDockerService(IConfiguration configuration, ILogger<DatabaseBackupService> logger, DockerClient dockerClient)
    {
        _configuration = configuration;
        _logger = logger;
        _dockerClient = dockerClient;
    }
    
    public async Task<string> CreateBackupAsync()
    {
         var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"backup_{timestamp}.dump");
        
        var networkName = _configuration["DOCKER_NETWORK"] ?? "scfet_network";
        try
        {
            var containerConfig = new CreateContainerParameters
            {
                Image = "postgres:15-alpine",
                Cmd = new List<string>
                {
                    "pg_dump",
                    $"--dbname=postgresql://{connectionStringBuilder.Username}:{connectionStringBuilder.Password}@postgres:5432/{connectionStringBuilder.Database}",
                    "--format=c",
                    "--file=/backup.dump"
                },
                HostConfig = new HostConfig
                {
                    NetworkMode = networkName 
                }
            };
            
            // Создаем временный контейнер
            var container = await _dockerClient.Containers.CreateContainerAsync(containerConfig);
            
            // Запускаем контейнер
            var started = await _dockerClient.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters());
            
            if (!started)
            {
                throw new Exception("Failed to start backup container");
            }
            
            // Ждем завершения
            await Task.Delay(TimeSpan.FromSeconds(10));
            
            // Копируем файл из контейнера
            var stream = await _dockerClient.Containers.GetArchiveFromContainerAsync(
                container.ID,
                new GetArchiveFromContainerParameters { Path = "/backup.dump" },
                false);
            using (var file = File.Create(backupPath))
            {
                await stream.Stream.CopyToAsync(file);
            }

            // Удаляем контейнер
            await _dockerClient.Containers.RemoveContainerAsync(
                container.ID,
                new ContainerRemoveParameters { Force = true });
            
            _logger.LogInformation($"Backup created: {backupPath}");
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup creation failed");
            throw;
        }
    }
}