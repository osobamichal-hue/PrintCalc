namespace PrintCalc.Infrastructure.Services.Backup;

public sealed class BackupSummary
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string AppVersion { get; init; } = "PrintCalc";
    public string DatabaseFileName { get; init; } = "printcalc.db";
    public long DatabaseSizeBytes { get; init; }
    public int Customers { get; init; }
    public int FilamentTypes { get; init; }
    public int FilamentStockItems { get; init; }
    public int Printers { get; init; }
    public int Models { get; init; }
    public int Calculations { get; init; }
    public int Quotes { get; init; }
    public int QuoteLines { get; init; }
    public int Orders { get; init; }
    public int OrderLines { get; init; }
    public int Invoices { get; init; }
    public int InvoiceLines { get; init; }
}

public sealed record BackupCreateResult(BackupSummary Summary, string TempZipPath);

public sealed record BackupRestoreResult(bool Success, string Message, BackupSummary? Manifest);
