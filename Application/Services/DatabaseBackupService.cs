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

public class SimpleDatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SimpleDatabaseBackupService> _logger;
    
    public SimpleDatabaseBackupService(IConfiguration configuration, ILogger<SimpleDatabaseBackupService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<string> CreateBackupAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"backup_{timestamp}.sql");
        
        try
        {
            _logger.LogInformation("Creating database backup...");
            
            // Используем pg_dump через локальный вызов
            await CreateBackupUsingPgDump(connectionString, backupPath);
            
            var fileInfo = new FileInfo(backupPath);
            _logger.LogInformation($"Backup created: {backupPath} ({fileInfo.Length} bytes)");
            
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            throw;
        }
    }

    private async Task CreateBackupUsingPgDump(string connectionString, string backupPath)
    {
        // Разбираем строку подключения
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        
        // Формируем команду для docker exec
        var dumpCommand = $"pg_dump --host={builder.Host} --port={builder.Port} " +
                          $"--username={builder.Username} --dbname={builder.Database} " +
                          $"--clean --create --format=p";
        
        // Выполняем через docker exec
        var processInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"exec scfet-postgres-1 sh -c \"PGPASSWORD='{builder.Password}' {dumpCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        
        using var process = Process.Start(processInfo);
        if (process == null)
            throw new Exception("Failed to start pg_dump process");
        
        // Читаем вывод и пишем в файл
        using var fileStream = File.Create(backupPath);
        using var writer = new StreamWriter(fileStream);
        
        // Асинхронно пишем stdout в файл
        var writeTask = process.StandardOutput.BaseStream.CopyToAsync(fileStream);
        
        // Читаем stderr для логов
        var errorTask = process.StandardError.ReadToEndAsync();
        
        // Ждем завершения
        await process.WaitForExitAsync();
        await writeTask;
        
        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new Exception($"pg_dump failed (exit code: {process.ExitCode}): {error}");
        }
    }
}

public class DockerSdkBackupService : IDatabaseBackupService
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