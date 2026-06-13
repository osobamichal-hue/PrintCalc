using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services.Backup;

public sealed class BackupService : IBackupService
{
    private readonly AppDbContext _db;

    public BackupService(AppDbContext db) => _db = db;

    public async Task<BackupCreateResult> CreateBackupZipAsync(string? appConfigPath, CancellationToken ct = default)
    {
        var root = await GetDataRootPathAsync(ct);
        Directory.CreateDirectory(root);

        var tempDir = Path.Combine(Path.GetTempPath(), "PrintCalcBackup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var dbBackupPath = Path.Combine(tempDir, "printcalc.db");
            await ExportDatabaseAsync(dbBackupPath, ct);

            var settingsDump = await _db.AppSettings.AsNoTracking()
                .OrderBy(x => x.Key)
                .ToDictionaryAsync(x => x.Key, x => x.Value, ct);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "appsettings-db.json"),
                JsonSerializer.Serialize(settingsDump, new JsonSerializerOptions { WriteIndented = true }),
                ct);

            var summary = await BuildSummaryAsync(dbBackupPath, ct);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "backup-manifest.json"),
                JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
                ct);

            if (!string.IsNullOrWhiteSpace(appConfigPath) && File.Exists(appConfigPath))
                File.Copy(appConfigPath, Path.Combine(tempDir, "appsettings.json"), true);

            var dataExportDir = Path.Combine(tempDir, "data-root");
            CopyDirectory(root, dataExportDir, excludeTopLevelDirectoryNames: ["Backups"]);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipPath = Path.Combine(Path.GetTempPath(), $"PrintCalc_Backup_{stamp}.zip");
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);

            return new BackupCreateResult(summary, zipPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    public async Task<BackupRestoreResult> RestoreFromZipAsync(Stream zipStream, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PrintCalcRestore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
                archive.ExtractToDirectory(tempDir, true);

            var restoreDbPath = Path.Combine(tempDir, "printcalc.db");
            if (!File.Exists(restoreDbPath))
                return new BackupRestoreResult(false, "Záloha neobsahuje soubor printcalc.db.", null);

            BackupSummary? manifest = null;
            var manifestPath = Path.Combine(tempDir, "backup-manifest.json");
            if (File.Exists(manifestPath))
            {
                manifest = JsonSerializer.Deserialize<BackupSummary>(
                    await File.ReadAllTextAsync(manifestPath, ct));
            }

            var currentDbPath = _db.Database.GetDbConnection().DataSource;
            if (string.IsNullOrWhiteSpace(currentDbPath))
                return new BackupRestoreResult(false, "Nepodařilo se zjistit cestu k databázi.", manifest);

            _db.ChangeTracker.Clear();
            await _db.Database.CloseConnectionAsync();

            File.Copy(restoreDbPath, currentDbPath, true);

            var restoreDataRoot = Path.Combine(tempDir, "data-root");
            var targetRoot = await GetDataRootPathAsync(ct);
            if (Directory.Exists(restoreDataRoot))
            {
                Directory.CreateDirectory(targetRoot);
                CopyDirectory(restoreDataRoot, targetRoot);
            }

            return new BackupRestoreResult(
                true,
                "Obnova dokončena. Doporučujeme restartovat API a obnovit stránku v prohlížeči.",
                manifest);
        }
        catch (Exception ex)
        {
            return new BackupRestoreResult(false, $"Obnova selhala: {ex.Message}", null);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    private async Task ExportDatabaseAsync(string dbBackupPath, CancellationToken ct)
    {
        var dbPath = _db.Database.GetDbConnection().DataSource;
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            throw new InvalidOperationException("Záloha databáze selhala — soubor printcalc.db nebyl nalezen.");

        _db.ChangeTracker.Clear();
        await _db.Database.CloseConnectionAsync();

        try
        {
            var escaped = dbBackupPath.Replace("'", "''");
            await _db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", Array.Empty<object>(), ct);
            if (File.Exists(dbBackupPath))
                return;
        }
        catch
        {
            /* fallback copy */
        }

        File.Copy(dbPath, dbBackupPath, true);
        if (!File.Exists(dbBackupPath))
            throw new InvalidOperationException("Záloha databáze selhala.");
    }

    private async Task<string> GetDataRootPathAsync(CancellationToken ct)
    {
        var value = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "App.DataRootPath")
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(value)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PrintCalc")
            : value;
    }

    private async Task<BackupSummary> BuildSummaryAsync(string dbBackupPath, CancellationToken ct)
    {
        var dbFile = new FileInfo(dbBackupPath);
        return new BackupSummary
        {
            CreatedAt = DateTime.UtcNow,
            DatabaseFileName = dbFile.Name,
            DatabaseSizeBytes = dbFile.Exists ? dbFile.Length : 0,
            Customers = await _db.Customers.AsNoTracking().CountAsync(ct),
            FilamentTypes = await _db.FilamentTypes.AsNoTracking().CountAsync(ct),
            FilamentStockItems = await _db.FilamentStocks.AsNoTracking().CountAsync(ct),
            Printers = await _db.Printers.AsNoTracking().CountAsync(ct),
            Models = await _db.PrintModels.AsNoTracking().CountAsync(ct),
            Calculations = await _db.Calculations.AsNoTracking().CountAsync(ct),
            Quotes = await _db.Quotes.AsNoTracking().CountAsync(ct),
            QuoteLines = await _db.QuoteLines.AsNoTracking().CountAsync(ct),
            Orders = await _db.Orders.AsNoTracking().CountAsync(ct),
            OrderLines = await _db.OrderLines.AsNoTracking().CountAsync(ct),
            Invoices = await _db.Invoices.AsNoTracking().CountAsync(ct),
            InvoiceLines = await _db.InvoiceLines.AsNoTracking().CountAsync(ct),
        };
    }

    private static void CopyDirectory(
        string sourceDir,
        string targetDir,
        IEnumerable<string>? excludeTopLevelDirectoryNames = null)
    {
        if (!Directory.Exists(sourceDir))
            return;
        Directory.CreateDirectory(targetDir);
        var excluded = new HashSet<string>(excludeTopLevelDirectoryNames ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (excluded.Contains(name))
                continue;
            CopyDirectory(dir, Path.Combine(targetDir, name));
        }
    }
}
