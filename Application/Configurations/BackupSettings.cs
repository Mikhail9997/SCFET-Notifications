namespace Application.Configurations;

public class BackupSettings
{
    public int IntervalHours { get; set; }
    public int MaxBackups { get; set; }
    public string BackupDirectory { get; set; } = string.Empty;
    public string PgDumpPath { get; set; } = string.Empty;
}