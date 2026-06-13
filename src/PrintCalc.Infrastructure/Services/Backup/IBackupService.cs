namespace PrintCalc.Infrastructure.Services.Backup;

public interface IBackupService
{
    Task<BackupCreateResult> CreateBackupZipAsync(string? appConfigPath, CancellationToken ct = default);
    Task<BackupRestoreResult> RestoreFromZipAsync(Stream zipStream, CancellationToken ct = default);
}
