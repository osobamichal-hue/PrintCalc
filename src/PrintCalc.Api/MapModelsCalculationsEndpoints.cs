using Microsoft.EntityFrameworkCore;
using PrintCalc.Api.Services;
using PrintCalc.Api.Util;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Helpers;
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

            var list = await query.Take(400).Select(m => PrintModelListDto.FromEntity(m)).ToListAsync(ct);
            return Results.Ok(list);
        });

        app.MapGet("/api/print-models/{id:int}/metadata", async (
            int id,
            int? filamentTypeId,
            AppDbContext db,
            IModelMetadataResolver resolver,
            CancellationToken ct) =>
        {
            var m = await db.PrintModels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();

            decimal density = 1.24m;
            if (filamentTypeId is int fid and > 0)
            {
                var d = await db.FilamentTypes.AsNoTracking()
                    .Where(f => f.Id == fid)
                    .Select(f => (decimal?)f.DensityGPerCm3)
                    .FirstOrDefaultAsync(ct);
                if (d is > 0) density = d.Value;
            }

            var temp = Path.Combine(Path.GetTempPath(), "pc-meta-" + Guid.NewGuid().ToString("N") + "." + m.FileType.ToLowerInvariant());
            try
            {
                await File.WriteAllBytesAsync(temp, m.FileContent, ct);
                var meta = resolver.Resolve(temp, density);
                return Results.Ok(PrintModelMetadataDto.From(m, meta, includeStoredWarnings: false));
            }
            finally
            {
                try { File.Delete(temp); } catch { /* ignore */ }
            }
        });

        app.MapGet("/api/print-models/{id:int}/file", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var m = await db.PrintModels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            var ext = m.FileType.ToLowerInvariant();
            var contentType = ext switch
            {
                "stl" => "application/octet-stream",
                "obj" => "application/octet-stream",
                "3mf" => "application/vnd.ms-package.3dmanufacturing-3dmodel+xml",
                "gcode" or "gco" => "text/plain",
                _ => "application/octet-stream"
            };
            return Results.File(m.FileContent, contentType, m.OriginalFileName);
        });

        app.MapPost("/api/print-models/{id:int}/reanalyze", async (
            int id,
            int? filamentTypeId,
            AppDbContext db,
            IModelMetadataResolver resolver,
            CancellationToken ct) =>
        {
            var m = await db.PrintModels.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();

            decimal density = 1.24m;
            if (filamentTypeId is int fid and > 0)
            {
                var d = await db.FilamentTypes.AsNoTracking()
                    .Where(f => f.Id == fid)
                    .Select(f => (decimal?)f.DensityGPerCm3)
                    .FirstOrDefaultAsync(ct);
                if (d is > 0) density = d.Value;
            }

            var ext = "." + m.FileType.ToLowerInvariant();
            if (ext == ".gco") ext = ".gcode";
            var temp = Path.Combine(Path.GetTempPath(), "pc-reanalyze-" + Guid.NewGuid().ToString("N") + ext);
            try
            {
                await File.WriteAllBytesAsync(temp, m.FileContent, ct);
                var meta = resolver.Resolve(temp, density);
                m.EstimatedMaterialGrams = meta.MaterialGrams;
                m.EstimatedPrintHours = meta.PrintHours;
                m.VolumeCm3 = meta.VolumeCm3;
                m.SurfaceCm2 = meta.SurfaceCm2;
                m.BboxXmm = meta.BboxXmm;
                m.BboxYmm = meta.BboxYmm;
                m.BboxZmm = meta.BboxZmm;
                m.EstimateSource = meta.EstimateSource;
                m.GeometryWarnings = meta.Warnings.Count == 0 ? null : string.Join("\n", meta.Warnings);
                await db.SaveChangesAsync(ct);
                return Results.Ok(PrintModelListDto.FromEntity(m));
            }
            finally
            {
                try { File.Delete(temp); } catch { /* ignore */ }
            }
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
            if (body.EstimatedMaterialGrams is not null || body.EstimatedPrintHours is not null)
                m.EstimateSource = EstimateSource.Manual;
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

        app.MapPost("/api/print-models", async (
            HttpRequest request,
            AppDbContext db,
            IModelMetadataResolver resolver,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Očekává se multipart/form-data." });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Chybí pole souboru „file“." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".stl" or ".obj" or ".3mf" or ".gcode" or ".gco"))
                return Results.BadRequest(new { error = "Povolené přípony: .stl, .obj, .3mf, .gcode, .gco" });

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var temp = Path.Combine(Path.GetTempPath(), "pc-upload-" + Guid.NewGuid().ToString("N") + ext);
            try
            {
                await File.WriteAllBytesAsync(temp, bytes, ct);
                var meta = resolver.Resolve(temp);

                var type = ext.TrimStart('.').ToUpperInvariant();
                if (type == "GCO") type = "GCODE";
                var item = new PrintModel
                {
                    Name = Path.GetFileNameWithoutExtension(file.FileName),
                    FileType = type,
                    FilePath = null,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    FileContent = bytes,
                    EstimatedMaterialGrams = meta.MaterialGrams,
                    EstimatedPrintHours = meta.PrintHours,
                    VolumeCm3 = meta.VolumeCm3,
                    SurfaceCm2 = meta.SurfaceCm2,
                    BboxXmm = meta.BboxXmm,
                    BboxYmm = meta.BboxYmm,
                    BboxZmm = meta.BboxZmm,
                    EstimateSource = meta.EstimateSource,
                    GeometryWarnings = meta.Warnings.Count == 0 ? null : string.Join("\n", meta.Warnings)
                };
                db.PrintModels.Add(item);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/print-models/{item.Id}/file", PrintModelListDto.FromEntity(item));
            }
            finally
            {
                try { File.Delete(temp); } catch { /* ignore */ }
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
            var calc = await MapToNewCalculationAsync(body, quote!, db, ct);
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
            await ApplyToCalculationAsync(tracked, body, quote!, db, ct);
            if (body.ModelDesignHourlyRate > 0)
                await AppSettingsQueries.UpsertAsync(db, "ModelingHourlyRate",
                    body.ModelDesignHourlyRate.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapPost("/api/calculations/{id:int}/issue-stock", async (int id, IStockService stock, CancellationToken ct) =>
        {
            try
            {
                await stock.IssueForCalculationAsync(id, ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

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
        var postProcessingDefault = await AppSettingsQueries.GetDecimalAsync(db, "Calculation.PostProcessingHourlyRate", 350m, ct);
        var tiersRaw = await AppSettingsQueries.GetStringAsync(db, "Calculation.QuantityDiscountTiers", "1:0;5:5;20:12", ct);
        var tiers = QuantityDiscountHelper.ParseTiers(tiersRaw);

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
        var postProcessingRate = body.PostProcessingHourlyRate > 0
            ? body.PostProcessingHourlyRate
            : postProcessingDefault;

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
            CustomerSuppliedMaterial = body.CustomerSuppliedMaterial,
            SlicingFeePerModel = Math.Max(0, body.SlicingFeePerModel),
            PostProcessingHours = Math.Max(0, body.PostProcessingHours),
            PostProcessingHourlyRate = postProcessingRate,
            WasteCoefficientPercent = Math.Max(0, body.WasteCoefficientPercent),
            QuantityDiscountTiers = tiers
        };

        return (engine.Compute(input), null);
    }

    private static async Task<Calculation> MapToNewCalculationAsync(
        CalculationSaveDto body,
        PriceQuote q,
        AppDbContext db,
        CancellationToken ct)
    {
        var electricity = await AppSettingsQueries.GetDecimalAsync(db, "ElectricityPricePerKwh", 7.5m, ct);
        var modelingDefault = await AppSettingsQueries.GetDecimalAsync(db, "ModelingHourlyRate", 450m, ct);
        var postProcessingDefault = await AppSettingsQueries.GetDecimalAsync(db, "Calculation.PostProcessingHourlyRate", 350m, ct);
        var calc = new Calculation
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
                ? (body.ModelDesignHourlyRate > 0 ? body.ModelDesignHourlyRate : modelingDefault)
                : 0,
            MarginPercent = body.MarginPercent,
            ElectricityPricePerKwh = electricity,
            SlicingFeePerModel = Math.Max(0, body.SlicingFeePerModel),
            PostProcessingHours = Math.Max(0, body.PostProcessingHours),
            PostProcessingHourlyRate = body.PostProcessingHourlyRate > 0 ? body.PostProcessingHourlyRate : postProcessingDefault,
            WasteCoefficientPercent = Math.Max(0, body.WasteCoefficientPercent),
            MaterialCost = q.MaterialCost,
            PrintCost = q.PrintCost,
            EnergyCost = q.EnergyCost,
            ModelDesignCost = q.ModelDesignCost,
            StartFeeCost = q.StartFeeCost,
            SlicingFeeCost = q.SlicingFeeCost,
            PostProcessingCost = q.PostProcessingCost,
            QuantityDiscountPercent = q.QuantityDiscountPercent,
            QuantityDiscountAmount = q.QuantityDiscountAmount,
            Subtotal = q.Subtotal,
            DiscountedSubtotal = q.DiscountedSubtotal,
            TotalWithMargin = q.TotalWithMargin,
            UnitPrice = q.UnitPriceForRequestedPiece,
            Title = string.IsNullOrWhiteSpace(body.Title) ? "Kalkulace" : body.Title.Trim(),
            QuotePrintDescriptionOverride = ApiStringUtil.TrimOrNull(body.QuotePrintDescriptionOverride)
        };
        return calc;
    }

    private static async Task ApplyToCalculationAsync(
        Calculation tracked,
        CalculationSaveDto body,
        PriceQuote q,
        AppDbContext db,
        CancellationToken ct)
    {
        var electricity = await AppSettingsQueries.GetDecimalAsync(db, "ElectricityPricePerKwh", 7.5m, ct);
        var modelingDefault = await AppSettingsQueries.GetDecimalAsync(db, "ModelingHourlyRate", 450m, ct);
        var postProcessingDefault = await AppSettingsQueries.GetDecimalAsync(db, "Calculation.PostProcessingHourlyRate", 350m, ct);

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
            ? (body.ModelDesignHourlyRate > 0 ? body.ModelDesignHourlyRate : modelingDefault)
            : 0;
        tracked.MarginPercent = body.MarginPercent;
        tracked.ElectricityPricePerKwh = electricity;
        tracked.SlicingFeePerModel = Math.Max(0, body.SlicingFeePerModel);
        tracked.PostProcessingHours = Math.Max(0, body.PostProcessingHours);
        tracked.PostProcessingHourlyRate = body.PostProcessingHourlyRate > 0 ? body.PostProcessingHourlyRate : postProcessingDefault;
        tracked.WasteCoefficientPercent = Math.Max(0, body.WasteCoefficientPercent);
        tracked.MaterialCost = q.MaterialCost;
        tracked.PrintCost = q.PrintCost;
        tracked.EnergyCost = q.EnergyCost;
        tracked.ModelDesignCost = q.ModelDesignCost;
        tracked.StartFeeCost = q.StartFeeCost;
        tracked.SlicingFeeCost = q.SlicingFeeCost;
        tracked.PostProcessingCost = q.PostProcessingCost;
        tracked.QuantityDiscountPercent = q.QuantityDiscountPercent;
        tracked.QuantityDiscountAmount = q.QuantityDiscountAmount;
        tracked.Subtotal = q.Subtotal;
        tracked.DiscountedSubtotal = q.DiscountedSubtotal;
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
    decimal? VolumeCm3,
    decimal? SurfaceCm2,
    decimal? BboxXmm,
    decimal? BboxYmm,
    decimal? BboxZmm,
    string EstimateSource,
    string? GeometryWarnings,
    string? Notes,
    DateTime CreatedAt)
{
    public static PrintModelListDto FromEntity(PrintModel m) => new(
        m.Id,
        m.Name,
        m.FileType,
        m.OriginalFileName,
        m.EstimatedMaterialGrams,
        m.EstimatedPrintHours,
        m.VolumeCm3,
        m.SurfaceCm2,
        m.BboxXmm,
        m.BboxYmm,
        m.BboxZmm,
        m.EstimateSource.ToString(),
        m.GeometryWarnings,
        m.Notes,
        m.CreatedAt);
}

public record PrintModelMetadataDto(
    int Id,
    decimal? EstimatedMaterialGrams,
    decimal? EstimatedPrintHours,
    decimal? VolumeCm3,
    decimal? SurfaceCm2,
    decimal? BboxXmm,
    decimal? BboxYmm,
    decimal? BboxZmm,
    string EstimateSource,
    string[] Warnings,
    string? PrinterFitWarning)
{
    public static PrintModelMetadataDto From(PrintModel m, ModelMetadataResult meta, bool includeStoredWarnings = true)
    {
        var warnings = meta.Warnings.ToArray();
        if (includeStoredWarnings && !string.IsNullOrWhiteSpace(m.GeometryWarnings))
            warnings = warnings.Concat(m.GeometryWarnings.Split('\n', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray();

        return new(
            m.Id,
            meta.MaterialGrams ?? m.EstimatedMaterialGrams,
            meta.PrintHours ?? m.EstimatedPrintHours,
            meta.VolumeCm3 ?? m.VolumeCm3,
            meta.SurfaceCm2 ?? m.SurfaceCm2,
            meta.BboxXmm ?? m.BboxXmm,
            meta.BboxYmm ?? m.BboxYmm,
            meta.BboxZmm ?? m.BboxZmm,
            (meta.EstimateSource != global::PrintCalc.Core.Enums.EstimateSource.Unknown ? meta.EstimateSource : m.EstimateSource).ToString(),
            warnings,
            null);
    }
}

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
    decimal SlicingFeePerModel,
    decimal PostProcessingHours,
    decimal PostProcessingHourlyRate,
    decimal WasteCoefficientPercent,
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
    decimal SlicingFeePerModel,
    decimal PostProcessingHours,
    decimal PostProcessingHourlyRate,
    decimal WasteCoefficientPercent,
    decimal MaterialCost,
    decimal PrintCost,
    decimal EnergyCost,
    decimal ModelDesignCost,
    decimal StartFeeCost,
    decimal SlicingFeeCost,
    decimal PostProcessingCost,
    decimal QuantityDiscountPercent,
    decimal QuantityDiscountAmount,
    decimal Subtotal,
    decimal DiscountedSubtotal,
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
        c.SlicingFeePerModel,
        c.PostProcessingHours,
        c.PostProcessingHourlyRate,
        c.WasteCoefficientPercent,
        c.MaterialCost,
        c.PrintCost,
        c.EnergyCost,
        c.ModelDesignCost,
        c.StartFeeCost,
        c.SlicingFeeCost,
        c.PostProcessingCost,
        c.QuantityDiscountPercent,
        c.QuantityDiscountAmount,
        c.Subtotal,
        c.DiscountedSubtotal,
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
    decimal SlicingFeeCost,
    decimal PostProcessingCost,
    decimal WasteCoefficientPercent,
    decimal QuantityDiscountPercent,
    decimal QuantityDiscountAmount,
    decimal Subtotal,
    decimal DiscountedSubtotal,
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
        q.SlicingFeeCost,
        q.PostProcessingCost,
        q.WasteCoefficientPercent,
        q.QuantityDiscountPercent,
        q.QuantityDiscountAmount,
        q.Subtotal,
        q.DiscountedSubtotal,
        q.TotalWithMargin,
        q.UnitPriceForRequestedPiece);
}
