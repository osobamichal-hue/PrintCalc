using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly AppDbContext _db;
    private readonly IStockService _stock;
    private readonly PurchaseInvoiceFileStorage _files;

    public PurchaseInvoiceService(AppDbContext db, IStockService stock, PurchaseInvoiceFileStorage files)
    {
        _db = db;
        _stock = stock;
        _files = files;
    }

    public async Task<PurchaseInvoice> CreateManualAsync(PurchaseInvoice invoice, CancellationToken ct = default)
    {
        invoice.Status = PurchaseInvoiceStatus.ReadyToMatch;
        invoice.ImportSource = PurchaseInvoiceImportSource.Manual;
        invoice.CreatedAt = DateTime.UtcNow;
        foreach (var line in invoice.Lines)
            PurchaseInvoiceLineCalculator.ComputeStockFields(line);

        invoice.TotalAmount = invoice.Lines.Sum(l => l.LineTotal);
        await EnsureSupplierAsync(invoice, ct);
        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<PurchaseInvoice> UpdateAsync(int id, PurchaseInvoice updated, CancellationToken ct = default)
    {
        var inv = await _db.PurchaseInvoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Faktura nenalezena.");

        if (inv.Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Zaúčtovanou fakturu nelze upravovat.");

        inv.Number = updated.Number.Trim();
        inv.IssueDate = updated.IssueDate;
        inv.DueDate = updated.DueDate;
        inv.SupplierName = updated.SupplierName.Trim();
        inv.SupplierCompanyId = updated.SupplierCompanyId?.Trim();
        inv.SupplierVatId = updated.SupplierVatId?.Trim();
        inv.Notes = updated.Notes?.Trim();
        inv.TotalAmount = updated.TotalAmount;

        _db.PurchaseInvoiceLines.RemoveRange(inv.Lines);
        inv.Lines.Clear();
        foreach (var line in updated.Lines)
        {
            PurchaseInvoiceLineCalculator.ComputeStockFields(line);
            line.PurchaseInvoiceId = id;
            inv.Lines.Add(line);
        }

        inv.TotalAmount = inv.Lines.Sum(l => l.LineTotal);
        await EnsureSupplierAsync(inv, ct);
        await _db.SaveChangesAsync(ct);
        return inv;
    }

    public async Task<PurchaseInvoice> ImportParsedAsync(ParsedPurchaseInvoice parsed, PurchaseInvoiceImportSource source, string? fileName, byte[]? fileBytes, CancellationToken ct = default)
    {
        var invoice = MapParsed(parsed, source);
        await EnsureSupplierAsync(invoice, ct);
        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        if (fileBytes is not null && !string.IsNullOrWhiteSpace(fileName))
            await _files.SaveSourceFileAsync(invoice.Id, fileName, fileBytes, ct);

        return invoice;
    }

    public async Task PostToStockAsync(int id, CancellationToken ct = default)
    {
        var inv = await _db.PurchaseInvoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Faktura nenalezena.");

        if (inv.Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Faktura je již zaúčtovaná na sklad.");

        if (inv.Lines.Any(l => l.FilamentTypeId is null))
            throw new InvalidOperationException("Všechny řádky musí být spárovány s typem filamentu.");

        if (inv.Lines.Any(l => l.WeightKg <= 0))
            throw new InvalidOperationException("Některé řádky mají nulovou hmotnost — zkontrolujte množství a jednotky.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var line in inv.Lines)
            {
                var note = $"FA {inv.Number}, {line.Description}";
                await _stock.ReceiveAsync(
                    line.FilamentTypeId!.Value,
                    line.WeightKg,
                    line.PricePerKg,
                    inv.SupplierName,
                    line.PieceCount,
                    line.Id,
                    null,
                    null,
                    note,
                    ct);
            }

            inv.Status = PurchaseInvoiceStatus.Posted;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var inv = await _db.PurchaseInvoices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (inv is null) return;
        if (inv.Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Zaúčtovanou fakturu nelze smazat.");

        _db.PurchaseInvoices.Remove(inv);
        await _db.SaveChangesAsync(ct);
    }

    private static PurchaseInvoice MapParsed(ParsedPurchaseInvoice parsed, PurchaseInvoiceImportSource source)
    {
        var invoice = new PurchaseInvoice
        {
            Number = string.IsNullOrWhiteSpace(parsed.Number) ? $"IMPORT-{DateTime.UtcNow:yyyyMMddHHmmss}" : parsed.Number.Trim(),
            IssueDate = parsed.IssueDate,
            DueDate = parsed.DueDate,
            SupplierName = parsed.SupplierName.Trim(),
            SupplierCompanyId = parsed.SupplierCompanyId?.Trim(),
            SupplierVatId = parsed.SupplierVatId?.Trim(),
            TotalAmount = parsed.TotalAmount,
            Status = PurchaseInvoiceStatus.ReadyToMatch,
            ImportSource = source,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var pl in parsed.Lines)
        {
            var line = new PurchaseInvoiceLine
            {
                Description = pl.Description.Trim(),
                Quantity = pl.Quantity,
                Unit = string.IsNullOrWhiteSpace(pl.Unit) ? "ks" : pl.Unit.Trim(),
                UnitPrice = pl.UnitPrice,
                TaxRatePercent = pl.TaxRatePercent,
                LineTotal = pl.LineTotal > 0 ? pl.LineTotal : pl.UnitPrice * pl.Quantity,
                ProductCode = pl.ProductCode?.Trim(),
                Ean = pl.Ean?.Trim()
            };
            PurchaseInvoiceLineCalculator.ComputeStockFields(line);
            invoice.Lines.Add(line);
        }

        if (invoice.TotalAmount <= 0)
            invoice.TotalAmount = invoice.Lines.Sum(l => l.LineTotal);

        return invoice;
    }

    private async Task EnsureSupplierAsync(PurchaseInvoice invoice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(invoice.SupplierCompanyId))
            return;

        var ico = invoice.SupplierCompanyId.Trim();
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.CompanyId == ico, ct);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = string.IsNullOrWhiteSpace(invoice.SupplierName) ? ico : invoice.SupplierName.Trim(),
                CompanyId = ico,
                VatId = invoice.SupplierVatId?.Trim()
            };
            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync(ct);
        }

        invoice.SupplierId = supplier.Id;
        if (string.IsNullOrWhiteSpace(invoice.SupplierName))
            invoice.SupplierName = supplier.Name;
    }
}
