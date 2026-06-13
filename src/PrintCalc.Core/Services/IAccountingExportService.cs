using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Services;

public interface IAccountingExportService
{
    Task WriteInvoiceCsvAsync(Invoice invoice, Stream utf8Stream, CancellationToken ct = default);
}
