using Microsoft.EntityFrameworkCore;
using PrintCalc.Api.Services;
using PrintCalc.Api.Util;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Api;

public static class MapModelsCalculationsEndpoints
{
    public static void MapModelsAndCalculations(this WebApplication app)
    {
        app.MapGet("/api/print-models", async (string? q, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.PrintModels.AsNoTracking().OrderByDescending(m => m.CreatedAt).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLowerInvariant();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(s) ||
                    m.OriginalFileName.ToLower().Contains(s) ||
                    (m.Notes ?? "").ToLower().Contains(s));
            }

            var list = await query.Take(400).Select(m => new PrintModelListDto(
                    m.Id,
                    m.Name,
                    m.FileType,
                    m.OriginalFileName,
                    m.EstimatedMaterialGrams,
                    m.EstimatedPrintHours,
                    m.Notes,
                    m.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        app.MapGet("/api/print-models/{id:int}/file", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var m = await db.PrintModels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            var ext = m.FileType.ToLowerInvariant();
            var contentType = ext switch
            {
                "stl" => "application/octet-stream",
                "3mf" => "application/vnd.ms-package.3dmanufacturing-3dmodel+xml",
                "gcode" or "gco" => "text/plain",
                _ => "application/octet-stream"
            };
            return Results.File(m.FileContent, contentType, m.OriginalFileName);
        });

        app.MapPut("/api/print-models/{id:int}", async (int id, PrintModelMetaWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var m = await db.PrintModels.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Název modelu je povinný." });
            m.Name = body.Name.Trim();
            m.EstimatedMaterialGrams = body.EstimatedMaterialGrams;
            m.EstimatedPrintHours = body.EstimatedPrintHours;
            m.Notes = ApiStringUtil.TrimOrNull(body.Notes);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/print-models/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var m = await db.PrintModels.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            db.PrintModels.Remove(m);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapPost("/api/print-models", async (HttpRequest request, AppDbContext db, IThreeMfReader threeMf, IGcodeReader gcode, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Očekává se multipart/form-data." });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Chybí pole souboru „file“." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".stl" or ".3mf" or ".gcode" or ".gco"))
                return Results.BadRequest(new { error = "Povolené přípony: .stl, .3mf, .gcode, .gco" });

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var temp = Path.Combine(Path.GetTempPath(), "pc-upload-" + Guid.NewGuid().ToString("N") + ext);
            try
            {
                await File.WriteAllBytesAsync(temp, bytes, ct);
                decimal? grams = null;
                decimal? hours = null;
                if (ext == ".3mf")
                {
                    var meta = threeMf.ReadMetadata(temp);
                    grams = meta.MaterialGrams;
                    hours = meta.PrintHours;
                }
                else if (ext is ".gcode" or ".gco")
                {
                    var meta = gcode.ReadMetadata(temp);
                    grams = meta.MaterialGrams;
                    hours = meta.PrintHours;
                }

                var type = ext.TrimStart('.').ToUpperInvariant();
                var item = new PrintModel
                {
                    Name = Path.GetFileNameWithoutExtension(file.FileName),
                    FileType = type,
                    FilePath = null,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    FileContent = bytes,
                    EstimatedMaterialGrams = grams,
                    EstimatedPrintHours = hours
                };
                db.PrintModels.Add(item);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/print-models/{item.Id}/file", new PrintModelListDto(
                    item.Id,
                    item.Name,
                    item.FileType,
                    item.OriginalFileName,
                    item.EstimatedMaterialGrams,
                    item.EstimatedPrintHours,
                    item.Notes,
                    item.CreatedAt));
            }
            finally
            {
                try { File.Delete(temp); }
                catch { /* ignore */ }
            }
        });

        app.MapPost("/api/calculations/preview", async (
            CalculationSaveDto body,
            AppDbContext db,
            ICalculationEngine engine,
            CancellationToken ct) =>
        {
            var (quote, err) = await TryComputeAsync(db, engine, body, ct);
            if (err is not null) return Results.BadRequest(new { error = err });
            return Results.Ok(PriceQuoteDto.From(quote!));
        });

        app.MapGet("/api/calculations", async (int? customerId, AppDbContext db, CancellationToken ct) =>
        {
            var q = db.Calculations.AsNoTracking().OrderByDescending(c => c.CreatedAt).AsQueryable();
            if (customerId is > 0)
                q = q.Where(c => c.CustomerId == customerId);
            var list = await q.Take(300).Select(c => new CalculationListDto(
                    c.Id,
                    c.Title,
                    c.CustomerId,
                    c.TotalWithMargin,
                    c.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        app.MapGet("/api/calculations/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Calculations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return c is null ? Results.NotFound() : Results.Ok(CalculationDetailDto.FromEntity(c));
        });

        app.MapPost("/api/calculations", async (
            CalculationSaveDto body,
            AppDbContext db,
            ICalculationEngine engine,
            CancellationToken ct) =>
        {
            var (quote, err) = await TryComputeAsync(db, engine, body, ct);
            if (err is not null) return Results.BadRequest(new { error = err });
            var electricity = await AppSettingsQueries.GetDecimalAsync(db, "ElectricityPricePerKwh", 7.5m, ct);
            var modelingDefault = await AppSettingsQueries.GetDecimalAsync(db, "ModelingHourlyRate", 450m, ct);
            var calc = MapToNewCalculation(body, quote!, electricity, modelingDefault);
            calc.CreatedAt = DateTime.UtcNow;
            if (body.ModelDesignHourlyRate > 0)
                await AppSettingsQueries.UpsertAsync(db, "ModelingHourlyRate",
                    body.ModelDesignHourlyRate.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
            db.Calculations.Add(calc);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/calculations/{calc.Id}", CalculationDetailDto.FromEntity(calc));
        });

        app.MapPut("/api/calculations/{id:int}", async (
            int id,
            CalculationSaveDto body,
            AppDbContext db,
            ICalculationEngine engine,
            CancellationToken ct) =>
        {
            var tracked = await db.Calculations.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (tracked is null) return Results.NotFound();
            var (quote, err) = await TryComputeAsync(db, engine, body, ct);
            if (err is not null) return Results.BadRequest(new { error = err });
            var electricity = await AppSettingsQueries.GetDecimalAsync(db, "ElectricityPricePerKwh", 7.5m, ct);
            var modelingDefault = await AppSettingsQueries.GetDecimalAsync(db, "ModelingHourlyRate", 450m, ct);
            ApplyToCalculation(tracked, body, quote!, electricity, modelingDefault);
            if (body.ModelDesignHourlyRate > 0)
                await AppSettingsQueries.UpsertAsync(db, "ModelingHourlyRate",
                    body.ModelDesignHourlyRate.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/calculations/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Calculations.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return Results.NotFound();
            await db.Quotes.Where(q => q.SourceCalculationId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.SourceCalculationId, (int?)null), ct);
            db.Calculations.Remove(c);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<(PriceQuote? quote, string? error)> TryComputeAsync(
        AppDbContext db,
        ICalculationEngine engine,
        CalculationSaveDto body,
        CancellationToken ct)
    {
        var electricity = await AppSettingsQueries.GetDecimalAsync(db, "ElectricityPricePerKwh", 7.5m, ct);
        var modelingSetting = await AppSettingsQueries.GetDecimalAsync(db, "ModelingHourlyRate", 450m, ct);
        decimal filamentPrice = 0;
        if (body.FilamentTypeId is { } fid)
        {
            filamentPrice = await db.FilamentTypes.AsNoTracking()
                .Where(f => f.Id == fid)
                .Select(f => f.AveragePricePerKg)
                .FirstOrDefaultAsync(ct);
        }

        Printer? printer = null;
        if (body.PrinterId is { } pid)
        {
            printer = await db.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (printer is null)
                return (null, "Neznámá tiskárna.");
        }

        var modelDesignH = body.IncludeModelDesign ? Math.Max(0, body.ModelDesignHours) : 0;
        var modelDesignRate = body.IncludeModelDesign
            ? (body.ModelDesignHourlyRate > 0 ? body.ModelDesignHourlyRate : modelingSetting)
            : 0;

        var input = new CalculationInput
        {
            MaterialGrams = body.MaterialGrams,
            PrintHours = Math.Max(0, body.PrintHours),
            PiecesPerBuild = body.PiecesPerBuild < 1 ? 1 : body.PiecesPerBuild,
            RequiredPieces = body.RequiredPieces < 1 ? 1 : body.RequiredPieces,
            FilamentPricePerKg = filamentPrice,
            PrinterHourlyRate = printer?.HourlyRate ?? 0,
            PrinterKwhPerHour = printer?.KwhPerHour ?? 0,
            ModelDesignHours = modelDesignH,
            ModelDesignHourlyRate = modelDesignRate,
            StartFeePerPrint = printer?.StartFeePerPrint ?? 0,
            ElectricityPricePerKwh = electricity,
            MarginPercent = body.MarginPercent,
            CustomerSuppliedMaterial = body.CustomerSuppliedMaterial
        };

        return (engine.Compute(input), null);
    }

    private static Calculation MapToNewCalculation(
        CalculationSaveDto body,
        PriceQuote q,
        decimal electricityPricePerKwh,
        decimal modelingHourlyDefault) =>
        new()
        {
            CustomerId = body.CustomerId,
            FilamentTypeId = body.FilamentTypeId,
            PrinterId = body.PrinterId,
            PrintModelId = body.PrintModelId,
            SourceModelPath = ApiStringUtil.TrimOrNull(body.SourceModelPath),
            MaterialGrams = body.MaterialGrams,
            PrintHours = Math.Max(0, body.PrintHours),
            PiecesPerBuild = body.PiecesPerBuild < 1 ? 1 : body.PiecesPerBuild,
            RequiredPieces = body.RequiredPieces < 1 ? 1 : body.RequiredPieces,
            PrintRuns = q.PrintRuns,
            CustomerSuppliedMaterial = body.CustomerSuppliedMaterial,
            IncludeModelDesign = body.IncludeModelDesign,
            ModelDesignHours = body.IncludeModelDesign ? Math.Max(0, body.ModelDesignHours) : 0,
            ModelDesignHourlyRate = body.IncludeModelDesign
                ? (body.ModelDesignHourlyRate > 0 ? body.ModelDesignHourlyRate : modelingHourlyDefault)
                : 0,
            MarginPercent = body.MarginPercent,
            ElectricityPricePerKwh = electricityPricePerKwh,
            MaterialCost = q.MaterialCost,
            PrintCost = q.PrintCost,
            EnergyCost = q.EnergyCost,
            ModelDesignCost = q.ModelDesignCost,
            StartFeeCost = q.StartFeeCost,
            Subtotal = q.Subtotal,
            TotalWithMargin = q.TotalWithMargin,
            UnitPrice = q.UnitPriceForRequestedPiece,
            Title = string.IsNullOrWhiteSpace(body.Title) ? "Kalkulace" : body.Title.Trim(),
            QuotePrintDescriptionOverride = ApiStringUtil.TrimOrNull(body.QuotePrintDescriptionOverride)
        };

    private static void ApplyToCalculation(
        Calculation tracked,
        CalculationSaveDto body,
        PriceQuote q,
        decimal electricityPricePerKwh,
        decimal modelingHourlyDefault)
    {
        tracked.CustomerId = body.CustomerId;
        tracked.FilamentTypeId = body.FilamentTypeId;
        tracked.PrinterId = body.PrinterId;
        tracked.PrintModelId = body.PrintModelId;
        tracked.SourceModelPath = ApiStringUtil.TrimOrNull(body.SourceModelPath);
        tracked.MaterialGrams = body.MaterialGrams;
        tracked.PrintHours = Math.Max(0, body.PrintHours);
        tracked.PiecesPerBuild = body.PiecesPerBuild < 1 ? 1 : body.PiecesPerBuild;
        tracked.RequiredPieces = body.RequiredPieces < 1 ? 1 : body.RequiredPieces;
        tracked.PrintRuns = q.PrintRuns;
        tracked.CustomerSuppliedMaterial = body.CustomerSuppliedMaterial;
        tracked.IncludeModelDesign = body.IncludeModelDesign;
        tracked.ModelDesignHours = body.IncludeModelDesign ? Math.Max(0, body.ModelDesignHours) : 0;
        tracked.ModelDesignHourlyRate = body.IncludeModelDesign
            ? (body.ModelDesignHourlyRate > 0 ? body.ModelDesignHourlyRate : modelingHourlyDefault)
            : 0;
        tracked.MarginPercent = body.MarginPercent;
        tracked.ElectricityPricePerKwh = electricityPricePerKwh;
        tracked.MaterialCost = q.MaterialCost;
        tracked.PrintCost = q.PrintCost;
        tracked.EnergyCost = q.EnergyCost;
        tracked.ModelDesignCost = q.ModelDesignCost;
        tracked.StartFeeCost = q.StartFeeCost;
        tracked.Subtotal = q.Subtotal;
        tracked.TotalWithMargin = q.TotalWithMargin;
        tracked.UnitPrice = q.UnitPriceForRequestedPiece;
        tracked.Title = string.IsNullOrWhiteSpace(body.Title) ? "Kalkulace" : body.Title.Trim();
        tracked.QuotePrintDescriptionOverride = ApiStringUtil.TrimOrNull(body.QuotePrintDescriptionOverride);
    }
}

public record PrintModelListDto(
    int Id,
    string Name,
    string FileType,
    string OriginalFileName,
    decimal? EstimatedMaterialGrams,
    decimal? EstimatedPrintHours,
    string? Notes,
    DateTime CreatedAt);

public record PrintModelMetaWriteDto(
    string Name,
    decimal? EstimatedMaterialGrams,
    decimal? EstimatedPrintHours,
    string? Notes);

public record CalculationSaveDto(
    int? CustomerId,
    int? FilamentTypeId,
    int? PrinterId,
    int? PrintModelId,
    string? SourceModelPath,
    decimal MaterialGrams,
    decimal PrintHours,
    int PiecesPerBuild,
    int RequiredPieces,
    decimal MarginPercent,
    bool CustomerSuppliedMaterial,
    bool IncludeModelDesign,
    decimal ModelDesignHours,
    decimal ModelDesignHourlyRate,
    string? Title,
    string? QuotePrintDescriptionOverride);

public record CalculationListDto(int Id, string Title, int? CustomerId, decimal TotalWithMargin, DateTime CreatedAt);

public record CalculationDetailDto(
    int Id,
    int? CustomerId,
    int? FilamentTypeId,
    int? PrinterId,
    int? PrintModelId,
    string? SourceModelPath,
    decimal MaterialGrams,
    decimal PrintHours,
    int PiecesPerBuild,
    int RequiredPieces,
    int PrintRuns,
    decimal MarginPercent,
    bool CustomerSuppliedMaterial,
    bool IncludeModelDesign,
    decimal ModelDesignHours,
    decimal ModelDesignHourlyRate,
    decimal MaterialCost,
    decimal PrintCost,
    decimal EnergyCost,
    decimal ModelDesignCost,
    decimal StartFeeCost,
    decimal Subtotal,
    decimal TotalWithMargin,
    decimal UnitPrice,
    string Title,
    string? QuotePrintDescriptionOverride,
    DateTime CreatedAt)
{
    public static CalculationDetailDto FromEntity(Calculation c) => new(
        c.Id,
        c.CustomerId,
        c.FilamentTypeId,
        c.PrinterId,
        c.PrintModelId,
        c.SourceModelPath,
        c.MaterialGrams,
        c.PrintHours,
        c.PiecesPerBuild,
        c.RequiredPieces,
        c.PrintRuns,
        c.MarginPercent,
        c.CustomerSuppliedMaterial,
        c.IncludeModelDesign,
        c.ModelDesignHours,
        c.ModelDesignHourlyRate,
        c.MaterialCost,
        c.PrintCost,
        c.EnergyCost,
        c.ModelDesignCost,
        c.StartFeeCost,
        c.Subtotal,
        c.TotalWithMargin,
        c.UnitPrice,
        c.Title,
        c.QuotePrintDescriptionOverride,
        c.CreatedAt);
}

public record PriceQuoteDto(
    int PiecesPerBuild,
    int RequiredPieces,
    int PrintRuns,
    decimal MaterialCost,
    decimal PrintCost,
    decimal EnergyCost,
    decimal ModelDesignCost,
    decimal StartFeeCost,
    decimal Subtotal,
    decimal TotalWithMargin,
    decimal UnitPriceForRequestedPiece)
{
    public static PriceQuoteDto From(PriceQuote q) => new(
        q.PiecesPerBuild,
        q.RequiredPieces,
        q.PrintRuns,
        q.MaterialCost,
        q.PrintCost,
        q.EnergyCost,
        q.ModelDesignCost,
        q.StartFeeCost,
        q.Subtotal,
        q.TotalWithMargin,
        q.UnitPriceForRequestedPiece);
}
