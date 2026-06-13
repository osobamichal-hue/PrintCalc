using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Services;

public interface IStockService
{
    Task ReceiveAsync(int filamentTypeId, decimal weightKg, decimal purchasePricePerKg, string? supplier, int pieceCount, string? lotNumber = null, DateTime? expirationDate = null, string? notes = null, CancellationToken ct = default);
    Task ReceiveAsync(int filamentTypeId, decimal weightKg, decimal purchasePricePerKg, string? supplier, int pieceCount, int? purchaseInvoiceLineId, string? lotNumber = null, DateTime? expirationDate = null, string? notes = null, CancellationToken ct = default);
    Task IssueAsync(int filamentTypeId, decimal weightKg, string? note, CancellationToken ct = default);
    Task AdjustInventoryAsync(int filamentTypeId, decimal newTotalRemainingKg, string? note, CancellationToken ct = default);
    Task RecalculateAveragePriceAsync(int filamentTypeId, CancellationToken ct = default);
}
