using Microsoft.EntityFrameworkCore;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public class PurchaseInvoiceFileStorage
{
    private readonly AppDbContext _db;

    public PurchaseInvoiceFileStorage(AppDbContext db) => _db = db;

    public async Task<string> GetRootAsync(CancellationToken ct = default)
    {
        var custom = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "App.DataRootPath")
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(custom))
            return custom.Trim();

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintCalc");
    }

    public async Task SaveSourceFileAsync(int invoiceId, string fileName, byte[] bytes, CancellationToken ct = default)
    {
        var root = await GetRootAsync(ct);
        var dir = Path.Combine(root, "purchase-invoices", invoiceId.ToString());
        Directory.CreateDirectory(dir);
        var safeName = Path.GetFileName(fileName);
        var path = Path.Combine(dir, safeName);
        await File.WriteAllBytesAsync(path, bytes, ct);

        var inv = await _db.PurchaseInvoices.FirstAsync(x => x.Id == invoiceId, ct);
        inv.SourceFileName = safeName;
        inv.SourceFilePath = path;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] bytes, string fileName)?> TryReadSourceFileAsync(int invoiceId, CancellationToken ct = default)
    {
        var inv = await _db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (inv?.SourceFilePath is null || !File.Exists(inv.SourceFilePath))
            return null;
        var bytes = await File.ReadAllBytesAsync(inv.SourceFilePath, ct);
        return (bytes, inv.SourceFileName ?? Path.GetFileName(inv.SourceFilePath));
    }
}
