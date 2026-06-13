using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;
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

    public async Task IssueAsync(int filamentTypeId, decimal weightKg, string? note, int? calculationId = null, CancellationToken ct = default)
    {
        if (weightKg <= 0) throw new ArgumentOutOfRangeException(nameof(weightKg));
        var remaining = await _db.FilamentStocks
            .Where(s => s.FilamentTypeId == filamentTypeId && s.RemainingWeightKg > 0)
            .OrderBy(s => s.ExpirationDate == null)
            .ThenBy(s => s.ExpirationDate)
            .ThenBy(s => s.ReceivedAt)
            .ToListAsync(ct);

        var toTake = weightKg;
        var movements = new List<StockMovement>();
        foreach (var batch in remaining)
        {
            if (toTake <= 0) break;
            var use = Math.Min(batch.RemainingWeightKg, toTake);
            batch.RemainingWeightKg -= use;
            toTake -= use;
            movements.Add(new StockMovement
            {
                FilamentTypeId = filamentTypeId,
                MovementType = StockMovementType.Issue,
                DeltaKg = -use,
                Note = note,
                FilamentStockId = batch.Id,
                CalculationId = calculationId
            });
        }

        if (toTake > 0.0001m)
            throw new InvalidOperationException("Nedostatek materiálu na skladě.");

        _db.StockMovements.AddRange(movements);
        await _db.SaveChangesAsync(ct);
        await RecalculateAveragePriceAsync(filamentTypeId, ct);
    }

    public async Task IssueForCalculationAsync(int calculationId, CancellationToken ct = default)
    {
        var calc = await _db.Calculations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == calculationId, ct)
            ?? throw new InvalidOperationException("Kalkulace nebyla nalezena.");

        if (calc.CustomerSuppliedMaterial)
            throw new InvalidOperationException("U materiálu zákazníka se skladový výdej neprovádí.");

        if (calc.FilamentTypeId is not { } filamentTypeId)
            throw new InvalidOperationException("Kalkulace nemá přiřazený typ filamentu.");

        if (await _db.StockMovements.AnyAsync(m =>
                m.CalculationId == calculationId && m.MovementType == StockMovementType.Issue, ct))
            throw new InvalidOperationException("Materiál pro tuto kalkulaci již byl vydán ze skladu.");

        var wasteMul = 1m + Math.Max(0, calc.WasteCoefficientPercent) / 100m;
        var printRuns = Math.Max(1, calc.PrintRuns);
        var kg = calc.MaterialGrams / 1000m * printRuns * wasteMul;
        if (kg <= 0)
            throw new InvalidOperationException("Kalkulace nemá zadanou hmotnost materiálu.");

        await IssueAsync(filamentTypeId, kg, $"Výdej z kalkulace #{calculationId}: {calc.Title}", calculationId, ct);
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
            await IssueAsync(filamentTypeId, -delta, note ?? "Inventura (úbytek)", null, ct);
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

    public async Task<IReadOnlyList<StockAlert>> GetAlertsAsync(int expiringWithinDays = 30, CancellationToken ct = default)
    {
        var alerts = new List<StockAlert>();
        var types = await _db.FilamentTypes.AsNoTracking().ToListAsync(ct);
        var stocks = await _db.FilamentStocks.AsNoTracking()
            .Where(s => s.RemainingWeightKg > 0)
            .ToListAsync(ct);

        foreach (var type in types)
        {
            if (type.MinStockKg <= 0) continue;
            var totalKg = stocks.Where(s => s.FilamentTypeId == type.Id).Sum(s => s.RemainingWeightKg);
            if (totalKg < type.MinStockKg)
            {
                alerts.Add(new StockAlert(
                    StockAlertKind.LowStock,
                    type.Id,
                    type.Name,
                    Math.Round(totalKg, 3, MidpointRounding.AwayFromZero),
                    type.MinStockKg,
                    null,
                    null,
                    null,
                    $"Nízká zásoba: {totalKg:0.###} kg (min. {type.MinStockKg:0.###} kg)"));
            }
        }

        var cutoff = DateTime.UtcNow.Date.AddDays(expiringWithinDays);
        foreach (var stock in stocks.Where(s => s.ExpirationDate is not null && s.ExpirationDate <= cutoff))
        {
            var typeName = types.FirstOrDefault(t => t.Id == stock.FilamentTypeId)?.Name ?? "?";
            alerts.Add(new StockAlert(
                StockAlertKind.ExpiringSoon,
                stock.FilamentTypeId,
                typeName,
                Math.Round(stock.RemainingWeightKg, 3, MidpointRounding.AwayFromZero),
                null,
                stock.Id,
                stock.LotNumber,
                stock.ExpirationDate,
                $"Šarže {stock.LotNumber ?? stock.Id.ToString()} expiruje {stock.ExpirationDate:dd.MM.yyyy} ({stock.RemainingWeightKg:0.###} kg)"));
        }

        return alerts
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.FilamentTypeName)
            .ToList();
    }
}
