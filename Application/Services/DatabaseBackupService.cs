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

public class DockerSdkBackupService: IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DockerSdkBackupService> _logger;
    private readonly DockerClient _dockerClient;
    
    public DockerSdkBackupService(
        IConfiguration configuration, 
        ILogger<DockerSdkBackupService> logger,
        DockerClient dockerClient)
    {
        _configuration = configuration;
        _logger = logger;
        _dockerClient = dockerClient;
    }
    
    public async Task<string> CreateBackupAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"backup_{timestamp}.sql");
        
        try
        {
            _logger.LogInformation($"Creating backup of database: {builder.Database}");
            
            // 1. Находим PostgreSQL контейнер
            var containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });
            
            var postgresContainer = containers.FirstOrDefault(c => 
                c.Names?.Any(n => n.Contains("postgres")) == true && 
                c.State == "running");
            
            if (postgresContainer == null)
                throw new Exception("PostgreSQL container not found or not running");
            
            var containerId = postgresContainer.ID;
            _logger.LogInformation($"Found PostgreSQL container: {containerId}");
            
            // 2. Выполняем pg_dump внутри контейнера
            var execConfig = new ContainerExecCreateParameters
            {
                Cmd = new List<string>
                {
                    "pg_dump",
                    "--host=localhost",
                    $"--port=5432",
                    $"--username={builder.Username}",
                    $"--dbname={builder.Database}",
                    "--clean",
                    "--create",
                    "--format=p",
                    "--inserts"
                },
                AttachStdout = true,
                AttachStderr = true,
                User = "postgres"
            };
            
            // Устанавливаем переменную окружения с паролем
            execConfig.Env = new List<string> { $"PGPASSWORD={builder.Password}" };
            
            var exec = await _dockerClient.Exec.ExecCreateContainerAsync(containerId, execConfig);
            _logger.LogInformation($"Created exec instance: {exec.ID}");
            
            // 3. Запускаем выполнение и получаем поток с данными
            using var stream = await _dockerClient.Exec.StartAndAttachContainerExecAsync(
                exec.ID, 
                false, 
                CancellationToken.None);
            
            // 4. Читаем stdout и пишем в файл
            using var fileStream = File.Create(backupPath);
            await stream.CopyOutputToAsync(
                null,          // stdin - не нужен
                fileStream,    // stdout -> в файл
                Stream.Null,   // stderr -> игнорируем (или пишем в лог)
                CancellationToken.None);
            
            // 5. Проверяем результат
            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length == 0)
            {
                throw new Exception($"Backup file is empty.");
            }
            
            _logger.LogInformation($"Backup created successfully: {backupPath} ({fileInfo.Length} bytes)");
            
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup creation failed");
            throw;
        }
    }
}

public class DockerBackupUniversalService : IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DockerBackupUniversalService> _logger;
    private readonly DockerClient _dockerClient;

    public DockerBackupUniversalService(IConfiguration configuration, ILogger<DockerBackupUniversalService> logger, DockerClient dockerClient)
    {
        _configuration = configuration;
        _logger = logger;
        _dockerClient = dockerClient;
    }

    public async Task<string> CreateBackupAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        Directory.CreateDirectory(backupDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"backup_{timestamp}.sql");
        
        try
        {
            _logger.LogInformation($"Starting backup for database: {builder.Database}");
            
            // 1. Получаем все контейнеры
            var containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });
            
            // Ищем контейнер с PostgreSQL
            var postgresContainer = containers.FirstOrDefault(c => 
                c.Names != null && 
                c.Names.Any(n => n.Contains("postgres")) && 
                c.State == "running");
            
            if (postgresContainer == null)
            {
                // Логируем все контейнеры для отладки
                _logger.LogInformation("Available containers:");
                foreach (var container in containers)
                {
                    _logger.LogInformation($"- {string.Join(",", container.Names)} (State: {container.State})");
                }
                throw new Exception("PostgreSQL container not found or not running");
            }
            
            _logger.LogInformation($"Found PostgreSQL container: {postgresContainer.ID}");
            
            // 2. Создаем команду для выполнения pg_dump
            var execCreate = new ContainerExecCreateParameters
            {
                Cmd = new List<string> 
                { 
                    "bash", 
                    "-c", 
                    $"PGPASSWORD='{builder.Password}' pg_dump " +
                    $"-h localhost " +
                    $"-p 5432 " +
                    $"-U {builder.Username} " +
                    $"-d {builder.Database} " +
                    "--clean --create --inserts 2>&1"
                },
                AttachStdout = true,
                AttachStderr = true,
                AttachStdin = false,
                Tty = false,
                User = "postgres"
            };
            
            var exec = await _dockerClient.Exec.ExecCreateContainerAsync(
                postgresContainer.ID, 
                execCreate,
                CancellationToken.None);
            
            _logger.LogInformation($"Created exec instance: {exec.ID}");
            
            string stdout, stderr;
            
            // 3. Запускаем выполнение
            using (var stream = await _dockerClient.Exec.StartAndAttachContainerExecAsync(
                exec.ID, 
                false, 
                CancellationToken.None))
            {
                var result = await stream.ReadOutputToEndAsync(CancellationToken.None);
                stdout = result.stdout;
                stderr = result.stderr;
            }
            
            // 4. Проверяем результат выполнения
            var inspect = await _dockerClient.Exec.InspectContainerExecAsync(exec.ID, CancellationToken.None);
            _logger.LogInformation($"Command exit code: {inspect.ExitCode}");
            
            if (inspect.ExitCode != 0)
            {
                throw new Exception($"pg_dump failed with exit code {inspect.ExitCode}. Stderr: {stderr}");
            }
            
            if (!string.IsNullOrEmpty(stderr))
            {
                _logger.LogWarning($"pg_dump warnings: {stderr}");
            }
            
            if (string.IsNullOrEmpty(stdout))
            {
                throw new Exception("pg_dump produced empty output");
            }
            
            // 5. Сохраняем результат в файл
            await File.WriteAllTextAsync(backupPath, stdout);
            
            // 6. Проверяем размер файла
            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length == 0)
            {
                throw new Exception($"Backup file is empty: {backupPath}");
            }
            
            _logger.LogInformation($"Backup created successfully: {backupPath} ({fileInfo.Length} bytes)");
            
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup creation failed");
            throw;
        }
    }
}

