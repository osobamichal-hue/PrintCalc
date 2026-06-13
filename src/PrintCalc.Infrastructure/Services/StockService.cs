using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly AppDbContext _db;

    public StockService(AppDbContext db) => _db = db;

    public async Task ReceiveAsync(int filamentTypeId, decimal weightKg, decimal purchasePricePerKg, string? supplier, int pieceCount, string? lotNumber = null, DateTime? expirationDate = null, string? notes = null, CancellationToken ct = default)
        => await ReceiveAsync(filamentTypeId, weightKg, purchasePricePerKg, supplier, pieceCount, null, lotNumber, expirationDate, notes, ct);

    public async Task ReceiveAsync(int filamentTypeId, decimal weightKg, decimal purchasePricePerKg, string? supplier, int pieceCount, int? purchaseInvoiceLineId, string? lotNumber = null, DateTime? expirationDate = null, string? notes = null, CancellationToken ct = default)
    {
        if (weightKg <= 0) throw new ArgumentOutOfRangeException(nameof(weightKg));
        var type = await _db.FilamentTypes.FirstOrDefaultAsync(t => t.Id == filamentTypeId, ct)
            ?? throw new InvalidOperationException("Neznámý typ filamentu.");

        var stock = new FilamentStock
        {
            FilamentTypeId = filamentTypeId,
            LotNumber = lotNumber,
            ExpirationDate = expirationDate,
            SupplierName = supplier,
            Notes = notes,
            PurchasePricePerKg = purchasePricePerKg,
            InitialWeightKg = weightKg,
            RemainingWeightKg = weightKg,
            PieceCount = pieceCount < 1 ? 1 : pieceCount,
            ReceivedAt = DateTime.UtcNow,
            PurchaseInvoiceLineId = purchaseInvoiceLineId
        };
        _db.FilamentStocks.Add(stock);
        await _db.SaveChangesAsync(ct);

        _db.StockMovements.Add(new StockMovement
        {
            FilamentTypeId = filamentTypeId,
            MovementType = StockMovementType.Receipt,
            DeltaKg = weightKg,
            UnitPricePerKg = purchasePricePerKg,
            Note = supplier,
            FilamentStockId = stock.Id,
            PurchaseInvoiceLineId = purchaseInvoiceLineId
        });
        await _db.SaveChangesAsync(ct);

        await RecalculateAveragePriceAsync(filamentTypeId, ct);
    }

    public async Task IssueAsync(int filamentTypeId, decimal weightKg, string? note, CancellationToken ct = default)
    {
        if (weightKg <= 0) throw new ArgumentOutOfRangeException(nameof(weightKg));
        var remaining = await _db.FilamentStocks
            .Where(s => s.FilamentTypeId == filamentTypeId && s.RemainingWeightKg > 0)
            .OrderBy(s => s.ReceivedAt)
            .ToListAsync(ct);

        var toTake = weightKg;
        foreach (var batch in remaining)
        {
            if (toTake <= 0) break;
            var use = Math.Min(batch.RemainingWeightKg, toTake);
            batch.RemainingWeightKg -= use;
            toTake -= use;
        }

        if (toTake > 0.0001m)
            throw new InvalidOperationException("Nedostatek materiálu na skladě.");

        _db.StockMovements.Add(new StockMovement
        {
            FilamentTypeId = filamentTypeId,
            MovementType = StockMovementType.Issue,
            DeltaKg = -weightKg,
            Note = note
        });
        await _db.SaveChangesAsync(ct);
        await RecalculateAveragePriceAsync(filamentTypeId, ct);
    }

    public async Task AdjustInventoryAsync(int filamentTypeId, decimal newTotalRemainingKg, string? note, CancellationToken ct = default)
    {
        if (newTotalRemainingKg < 0) throw new ArgumentOutOfRangeException(nameof(newTotalRemainingKg));

        var current = await _db.FilamentStocks
            .Where(s => s.FilamentTypeId == filamentTypeId)
            .SumAsync(s => s.RemainingWeightKg, ct);

        var delta = newTotalRemainingKg - current;
        if (Math.Abs(delta) < 0.000001m) return;

        if (delta < 0)
        {
            await IssueAsync(filamentTypeId, -delta, note ?? "Inventura (úbytek)", ct);
            return;
        }

        var type = await _db.FilamentTypes.AsNoTracking().FirstAsync(t => t.Id == filamentTypeId, ct);
        var price = type.AveragePricePerKg;
        await ReceiveAsync(filamentTypeId, delta, price, "Inventura (příplatek)", 1, null, null, note, ct);
    }

    public async Task RecalculateAveragePriceAsync(int filamentTypeId, CancellationToken ct = default)
    {
        var type = await _db.FilamentTypes.FirstAsync(t => t.Id == filamentTypeId, ct);
        var batches = await _db.FilamentStocks
            .Where(s => s.FilamentTypeId == filamentTypeId && s.RemainingWeightKg > 0)
            .ToListAsync(ct);

        var totalKg = batches.Sum(s => s.RemainingWeightKg);
        if (totalKg <= 0)
        {
            type.AveragePricePerKg = 0;
        }
        else
        {
            var value = batches.Sum(s => s.RemainingWeightKg * s.PurchasePricePerKg);
            type.AveragePricePerKg = Math.Round(value / totalKg, 4, MidpointRounding.AwayFromZero);
        }

        await _db.SaveChangesAsync(ct);
    }
}
