using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IInvoiceImportService
{
    Task<ParsedPurchaseInvoice> ParseAsync(Stream stream, string fileName, PurchaseInvoiceImportSource? formatHint = null, CancellationToken ct = default);
    PurchaseInvoiceImportSource DetectFormat(string fileName, Stream? peekStream = null);
}
