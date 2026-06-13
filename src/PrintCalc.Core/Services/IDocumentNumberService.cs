namespace PrintCalc.Core.Services;

public interface IDocumentNumberService
{
    Task<string> NextQuoteNumberAsync(CancellationToken ct = default);
    Task<string> NextOrderNumberAsync(CancellationToken ct = default);
    Task<string> NextInvoiceNumberAsync(string? prefixOverride = null, CancellationToken ct = default);
}
