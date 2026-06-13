using Microsoft.EntityFrameworkCore;
using PrintCalc.Api.Util;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Api;

public static class MapInventoryEndpoints
{
    public static void MapInventory(this WebApplication app)
    {
        app.MapGet("/api/printers", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Printers.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
            return Results.Ok(list.Select(PrinterResponse.FromEntity));
        });

        app.MapGet("/api/printers/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var p = await db.Printers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return p is null ? Results.NotFound() : Results.Ok(PrinterResponse.FromEntity(p));
        });

        app.MapPost("/api/printers", async (PrinterWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Vyplňte název tiskárny." });
            var p = new Printer
            {
                Name = body.Name.Trim(),
                Kind = body.Kind,
                HourlyRate = body.HourlyRate,
                KwhPerHour = body.KwhPerHour,
                StartFeePerPrint = body.StartFeePerPrint,
                MaxVolumeDescription = ApiStringUtil.TrimOrNull(body.MaxVolumeDescription),
                Notes = ApiStringUtil.TrimOrNull(body.Notes)
            };
            db.Printers.Add(p);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/printers/{p.Id}", PrinterResponse.FromEntity(p));
        });

        app.MapPut("/api/printers/{id:int}", async (int id, PrinterWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var p = await db.Printers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Vyplňte název tiskárny." });
            p.Name = body.Name.Trim();
            p.Kind = body.Kind;
            p.HourlyRate = body.HourlyRate;
            p.KwhPerHour = body.KwhPerHour;
            p.StartFeePerPrint = body.StartFeePerPrint;
            p.MaxVolumeDescription = ApiStringUtil.TrimOrNull(body.MaxVolumeDescription);
            p.Notes = ApiStringUtil.TrimOrNull(body.Notes);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/printers/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var p = await db.Printers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null) return Results.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.Calculations
                    .Where(c => c.PrinterId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.PrinterId, (int?)null), ct);

                await db.PrinterFilamentTypes
                    .Where(pf => pf.PrinterId == id)
                    .ExecuteDeleteAsync(ct);

                db.Printers.Remove(p);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Results.NoContent();
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                return Results.Conflict(new
                {
                    error = "Tiskárnu nelze smazat — je navázána na jiná data v databázi."
                });
            }
        });

        app.MapGet("/api/filament-types", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.FilamentTypes.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);
            return Results.Ok(list.Select(FilamentTypeResponse.FromEntity));
        });

        app.MapGet("/api/filament-types/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.FilamentTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return t is null ? Results.NotFound() : Results.Ok(FilamentTypeResponse.FromEntity(t));
        });

        app.MapPost("/api/filament-types", async (FilamentTypeWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Vyplňte název typu." });
            var t = new FilamentType
            {
                Name = body.Name.Trim(),
                Manufacturer = ApiStringUtil.TrimOrNull(body.Manufacturer),
                DiameterMm = body.DiameterMm <= 0 ? 1.75m : body.DiameterMm,
                Color = ApiStringUtil.TrimOrNull(body.Color),
                DensityGPerCm3 = body.DensityGPerCm3 <= 0 ? 1.24m : body.DensityGPerCm3,
                NozzleTempMinC = body.NozzleTempMinC,
                NozzleTempMaxC = body.NozzleTempMaxC,
                BedTempMinC = body.BedTempMinC,
                BedTempMaxC = body.BedTempMaxC,
                Notes = ApiStringUtil.TrimOrNull(body.Notes),
                AveragePricePerKg = 0
            };
            db.FilamentTypes.Add(t);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/filament-types/{t.Id}", FilamentTypeResponse.FromEntity(t));
        });

        app.MapPut("/api/filament-types/{id:int}", async (int id, FilamentTypeWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.FilamentTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Vyplňte název typu." });
            t.Name = body.Name.Trim();
            t.Manufacturer = ApiStringUtil.TrimOrNull(body.Manufacturer);
            t.DiameterMm = body.DiameterMm <= 0 ? 1.75m : body.DiameterMm;
            t.Color = ApiStringUtil.TrimOrNull(body.Color);
            t.DensityGPerCm3 = body.DensityGPerCm3 <= 0 ? 1.24m : body.DensityGPerCm3;
            t.NozzleTempMinC = body.NozzleTempMinC;
            t.NozzleTempMaxC = body.NozzleTempMaxC;
            t.BedTempMinC = body.BedTempMinC;
            t.BedTempMaxC = body.BedTempMaxC;
            t.Notes = ApiStringUtil.TrimOrNull(body.Notes);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/filament-types/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var movements = await db.StockMovements.Where(m => m.FilamentTypeId == id).ToListAsync(ct);
                db.StockMovements.RemoveRange(movements);
                var stocks = await db.FilamentStocks.Where(s => s.FilamentTypeId == id).ToListAsync(ct);
                db.FilamentStocks.RemoveRange(stocks);
                var printerLinks = await db.PrinterFilamentTypes.Where(p => p.FilamentTypeId == id).ToListAsync(ct);
                db.PrinterFilamentTypes.RemoveRange(printerLinks);
                await db.Calculations
                    .Where(c => c.FilamentTypeId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.FilamentTypeId, (int?)null), ct);
                var tracked = await db.FilamentTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (tracked is not null)
                    db.FilamentTypes.Remove(tracked);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            return Results.NoContent();
        });

        app.MapGet("/api/filament-stocks", async (int? typeId, bool? activeOnly, AppDbContext db, CancellationToken ct) =>
        {
            var q = db.FilamentStocks.AsNoTracking().Include(s => s.FilamentType).AsQueryable();
            if (typeId is > 0)
                q = q.Where(s => s.FilamentTypeId == typeId.Value);
            if (activeOnly == true)
                q = q.Where(s => s.RemainingWeightKg > 0);
            var list = await q.OrderByDescending(s => s.ReceivedAt).ToListAsync(ct);
            return Results.Ok(list.Select(FilamentStockResponse.FromEntity));
        });

        app.MapGet("/api/stock-movements", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.StockMovements.AsNoTracking()
                .Include(m => m.FilamentType)
                .OrderByDescending(m => m.OccurredAt)
                .Take(500)
                .ToListAsync(ct);
            return Results.Ok(list.Select(m => new StockMovementResponse(
                m.Id,
                m.FilamentTypeId,
                m.FilamentType.Name,
                m.MovementType.ToString(),
                m.DeltaKg,
                m.UnitPricePerKg,
                m.Note,
                m.OccurredAt,
                m.FilamentStockId)));
        });

        app.MapPost("/api/stock/receive", async (ReceiveStockDto body, IStockService stock, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.FilamentTypes.AsNoTracking().AnyAsync(f => f.Id == body.FilamentTypeId, ct))
                return Results.BadRequest(new { error = "Neznámý typ filamentu." });
            try
            {
                await stock.ReceiveAsync(
                    body.FilamentTypeId,
                    body.WeightKg,
                    body.PurchasePricePerKg,
                    ApiStringUtil.TrimOrNull(body.Supplier),
                    body.PieceCount < 1 ? 1 : body.PieceCount,
                    ApiStringUtil.TrimOrNull(body.LotNumber),
                    body.ExpirationDate,
                    ApiStringUtil.TrimOrNull(body.Notes),
                    ct);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest(new { error = "Neplatné množství." });
            }

            return Results.NoContent();
        });

        app.MapPost("/api/stock/issue", async (IssueStockDto body, IStockService stock, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.FilamentTypes.AsNoTracking().AnyAsync(f => f.Id == body.FilamentTypeId, ct))
                return Results.BadRequest(new { error = "Neznámý typ filamentu." });
            try
            {
                await stock.IssueAsync(body.FilamentTypeId, body.WeightKg, ApiStringUtil.TrimOrNull(body.Note) ?? "Výdej", ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            return Results.NoContent();
        });
    }
}

public record PrinterWriteDto(
    string Name,
    PrinterKind Kind,
    decimal HourlyRate,
    decimal KwhPerHour,
    decimal StartFeePerPrint,
    string? MaxVolumeDescription,
    string? Notes);

public record PrinterResponse(
    int Id,
    string Name,
    string Kind,
    decimal HourlyRate,
    decimal KwhPerHour,
    decimal StartFeePerPrint,
    string? MaxVolumeDescription,
    string? Notes)
{
    public static PrinterResponse FromEntity(Printer p) => new(
        p.Id,
        p.Name,
        p.Kind.ToString(),
        p.HourlyRate,
        p.KwhPerHour,
        p.StartFeePerPrint,
        p.MaxVolumeDescription,
        p.Notes);
}

public record FilamentTypeWriteDto(
    string Name,
    string? Manufacturer,
    decimal DiameterMm,
    string? Color,
    decimal DensityGPerCm3,
    int? NozzleTempMinC,
    int? NozzleTempMaxC,
    int? BedTempMinC,
    int? BedTempMaxC,
    string? Notes);

public record FilamentTypeResponse(
    int Id,
    string Name,
    string? Manufacturer,
    decimal DiameterMm,
    string? Color,
    decimal DensityGPerCm3,
    int? NozzleTempMinC,
    int? NozzleTempMaxC,
    int? BedTempMinC,
    int? BedTempMaxC,
    decimal AveragePricePerKg,
    string? Notes)
{
    public static FilamentTypeResponse FromEntity(FilamentType t) => new(
        t.Id,
        t.Name,
        t.Manufacturer,
        t.DiameterMm,
        t.Color,
        t.DensityGPerCm3,
        t.NozzleTempMinC,
        t.NozzleTempMaxC,
        t.BedTempMinC,
        t.BedTempMaxC,
        t.AveragePricePerKg,
        t.Notes);
}

public record FilamentStockResponse(
    int Id,
    int FilamentTypeId,
    string FilamentTypeName,
    string? LotNumber,
    DateTime? ExpirationDate,
    string? SupplierName,
    string? Notes,
    decimal PurchasePricePerKg,
    decimal InitialWeightKg,
    decimal RemainingWeightKg,
    int PieceCount,
    DateTime ReceivedAt)
{
    public static FilamentStockResponse FromEntity(FilamentStock s) => new(
        s.Id,
        s.FilamentTypeId,
        s.FilamentType.Name,
        s.LotNumber,
        s.ExpirationDate,
        s.SupplierName,
        s.Notes,
        s.PurchasePricePerKg,
        s.InitialWeightKg,
        s.RemainingWeightKg,
        s.PieceCount,
        s.ReceivedAt);
}

public record StockMovementResponse(
    int Id,
    int FilamentTypeId,
    string FilamentTypeName,
    string MovementType,
    decimal DeltaKg,
    decimal? UnitPricePerKg,
    string? Note,
    DateTime OccurredAt,
    int? FilamentStockId);

public record ReceiveStockDto(
    int FilamentTypeId,
    decimal WeightKg,
    decimal PurchasePricePerKg,
    string? Supplier,
    int PieceCount,
    string? LotNumber,
    DateTime? ExpirationDate,
    string? Notes);

public record IssueStockDto(int FilamentTypeId, decimal WeightKg, string? Note);
