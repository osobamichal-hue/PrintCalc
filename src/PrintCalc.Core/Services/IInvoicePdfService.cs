using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Services;

public interface IInvoicePdfService
{
    Task<string> SaveInvoicePdfAsync(Invoice invoice, string outputDirectory, CancellationToken ct = default);
}
