using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IPdfInvoiceExtractor
{
    Task<ParsedPurchaseInvoice> ExtractAsync(Stream pdfStream, CancellationToken ct = default);
}
