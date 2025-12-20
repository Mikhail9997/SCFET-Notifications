using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController: ControllerBase
{
    private readonly IDatabaseBackupService _backupService;
    
    public BackupController(IDatabaseBackupService backupService)
    {
        _backupService = backupService;
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateBackup()
    {
        try
        {
            var backupPath = await _backupService.CreateBackupAsync();
            return Ok(new { 
                Message = "Backup created successfully", 
                Path = backupPath,
                FileName = Path.GetFileName(backupPath)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }
    
    [HttpGet("list")]
    public IActionResult ListBackups()
    {
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
            return Ok(Array.Empty<string>());
        
        var backups = Directory.GetFiles(backupDir)
            .Select(f => new BackupFile
            {
                Name = Path.GetFileName(f),
                Path = f,
                Size = new FileInfo(f).Length,
                Created = System.IO.File.GetCreationTime(f)
            })
            .OrderByDescending(f => f.Created)
            .ToList();
        
        return Ok(backups);
    }
    
    [HttpGet("download/{fileName}")]
    public IActionResult DownloadBackup(string fileName)
    {
        var backupPath = Path.Combine(Directory.GetCurrentDirectory(), "Backups", fileName);
        
        if (!System.IO.File.Exists(backupPath))
            return NotFound();
        
        var fileStream = System.IO.File.OpenRead(backupPath);
        return File(fileStream, "application/octet-stream", fileName);
    }
}

public class BackupFile
{
    public string Name { get; set; }
    public string Path { get; set; }
    public long Size { get; set; }
    public DateTime Created { get; set; }
}