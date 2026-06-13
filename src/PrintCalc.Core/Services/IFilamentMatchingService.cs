namespace PrintCalc.Core.Services;

public interface IFilamentMatchingService
{
    Task MatchLinesAsync(int purchaseInvoiceId, CancellationToken ct = default);
    Task SetManualMatchAsync(int lineId, int filamentTypeId, CancellationToken ct = default);
    Task<int> CreateFilamentTypeFromLineAsync(int lineId, CancellationToken ct = default);
}
