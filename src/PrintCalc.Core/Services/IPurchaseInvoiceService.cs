using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IPurchaseInvoiceService
{
    Task<PurchaseInvoice> CreateManualAsync(PurchaseInvoice invoice, CancellationToken ct = default);
    Task<PurchaseInvoice> UpdateAsync(int id, PurchaseInvoice invoice, CancellationToken ct = default);
    Task<PurchaseInvoice> ImportParsedAsync(ParsedPurchaseInvoice parsed, PurchaseInvoiceImportSource source, string? fileName, byte[]? fileBytes, CancellationToken ct = default);
    Task PostToStockAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
