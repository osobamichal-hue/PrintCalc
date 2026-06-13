using Microsoft.EntityFrameworkCore;
using PrintCalc.Api.Util;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using PrintCalc.Infrastructure.Services.PurchaseInvoices;

namespace PrintCalc.Api;

public static class MapPurchaseInvoicesEndpoints
{
    public static void MapPurchaseInvoices(this WebApplication app)
    {
        app.MapGet("/api/purchase-invoices", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.PurchaseInvoices.AsNoTracking()
                .Include(x => x.Lines)
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct);
            return Results.Ok(list.Select(PurchaseInvoiceListResponse.FromEntity));
        });

        app.MapGet("/api/purchase-invoices/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var inv = await db.PurchaseInvoices.AsNoTracking()
                .Include(x => x.Lines).ThenInclude(l => l.FilamentType)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return inv is null ? Results.NotFound() : Results.Ok(PurchaseInvoiceDetailResponse.FromEntity(inv));
        });

        app.MapPost("/api/purchase-invoices", async (PurchaseInvoiceWriteDto body, IPurchaseInvoiceService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Number))
                return Results.BadRequest(new { error = "Vyplňte číslo faktury." });
            if (body.Lines is null || body.Lines.Count == 0)
                return Results.BadRequest(new { error = "Přidejte alespoň jeden řádek." });

            var inv = body.ToEntity();
            var created = await svc.CreateManualAsync(inv, ct);
            return Results.Created($"/api/purchase-invoices/{created.Id}", PurchaseInvoiceDetailResponse.FromEntity(created));
        });

        app.MapPut("/api/purchase-invoices/{id:int}", async (int id, PurchaseInvoiceWriteDto body, IPurchaseInvoiceService svc, CancellationToken ct) =>
        {
            try
            {
                var inv = body.ToEntity();
                var updated = await svc.UpdateAsync(id, inv, ct);
                return Results.Ok(PurchaseInvoiceDetailResponse.FromEntity(updated));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/purchase-invoices/{id:int}", async (int id, IPurchaseInvoiceService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.DeleteAsync(id, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/purchase-invoices/import", async (
            HttpRequest request,
            IInvoiceImportService import,
            IPurchaseInvoiceService purchase,
            IFilamentMatchingService matching,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Očekáván multipart/form-data se souborem." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Nahrajte soubor." });

            PurchaseInvoiceImportSource? hint = null;
            if (form.TryGetValue("formatHint", out var hintVal) && Enum.TryParse<PurchaseInvoiceImportSource>(hintVal, true, out var parsedHint))
                hint = parsedHint;

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            try
            {
                using var parseStream = new MemoryStream(bytes);
                var parsed = await import.ParseAsync(parseStream, file.FileName, hint, ct);
                var source = hint ?? import.DetectFormat(file.FileName, new MemoryStream(bytes));
                var created = await purchase.ImportParsedAsync(parsed, source, file.FileName, bytes, ct);
                await matching.MatchLinesAsync(created.Id, ct);

                var inv = await db.PurchaseInvoices.AsNoTracking()
                    .Include(x => x.Lines).ThenInclude(l => l.FilamentType)
                    .FirstAsync(x => x.Id == created.Id, ct);
                return Results.Created($"/api/purchase-invoices/{created.Id}", PurchaseInvoiceDetailResponse.FromEntity(inv));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapPost("/api/purchase-invoices/{id:int}/match", async (int id, IFilamentMatchingService matching, AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                await matching.MatchLinesAsync(id, ct);
                var inv = await db.PurchaseInvoices.AsNoTracking()
                    .Include(x => x.Lines).ThenInclude(l => l.FilamentType)
                    .FirstAsync(x => x.Id == id, ct);
                return Results.Ok(PurchaseInvoiceDetailResponse.FromEntity(inv));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPut("/api/purchase-invoices/{id:int}/lines/{lineId:int}/match", async (
            int id, int lineId, ManualMatchDto body, IFilamentMatchingService matching, AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                await matching.SetManualMatchAsync(lineId, body.FilamentTypeId, ct);
                var inv = await db.PurchaseInvoices.AsNoTracking()
                    .Include(x => x.Lines).ThenInclude(l => l.FilamentType)
                    .FirstAsync(x => x.Id == id, ct);
                return Results.Ok(PurchaseInvoiceDetailResponse.FromEntity(inv));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/purchase-invoices/{id:int}/lines/{lineId:int}/create-filament-type", async (
            int id, int lineId, IFilamentMatchingService matching, AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                var typeId = await matching.CreateFilamentTypeFromLineAsync(lineId, ct);
                var inv = await db.PurchaseInvoices.AsNoTracking()
                    .Include(x => x.Lines).ThenInclude(l => l.FilamentType)
                    .FirstAsync(x => x.Id == id, ct);
                return Results.Ok(new { filamentTypeId = typeId, invoice = PurchaseInvoiceDetailResponse.FromEntity(inv) });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/purchase-invoices/{id:int}/post-to-stock", async (int id, IPurchaseInvoiceService svc, AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                await svc.PostToStockAsync(id, ct);
                var inv = await db.PurchaseInvoices.AsNoTracking()
                    .Include(x => x.Lines).ThenInclude(l => l.FilamentType)
                    .FirstAsync(x => x.Id == id, ct);
                return Results.Ok(PurchaseInvoiceDetailResponse.FromEntity(inv));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/purchase-invoices/{id:int}/source-file", async (int id, PurchaseInvoiceFileStorage files, AppDbContext db, CancellationToken ct) =>
        {
            var inv = await db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (inv?.SourceFilePath is null || !File.Exists(inv.SourceFilePath))
                return Results.NotFound();

            var bytes = await File.ReadAllBytesAsync(inv.SourceFilePath, ct);
            var name = inv.SourceFileName ?? Path.GetFileName(inv.SourceFilePath);
            var contentType = name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf"
                : name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? "application/xml"
                : "application/octet-stream";
            return Results.File(bytes, contentType, name);
        });
    }

    private sealed record ManualMatchDto(int FilamentTypeId);

    private sealed record PurchaseInvoiceWriteDto(
        string Number,
        DateTime IssueDate,
        DateTime? DueDate,
        string SupplierName,
        string? SupplierCompanyId,
        string? SupplierVatId,
        string? Notes,
        List<PurchaseInvoiceLineWriteDto> Lines)
    {
        public PurchaseInvoice ToEntity()
        {
            var inv = new PurchaseInvoice
            {
                Number = Number.Trim(),
                IssueDate = IssueDate,
                DueDate = DueDate,
                SupplierName = SupplierName.Trim(),
                SupplierCompanyId = ApiStringUtil.TrimOrNull(SupplierCompanyId),
                SupplierVatId = ApiStringUtil.TrimOrNull(SupplierVatId),
                Notes = ApiStringUtil.TrimOrNull(Notes)
            };
            foreach (var l in Lines)
            {
                inv.Lines.Add(new PurchaseInvoiceLine
                {
                    Description = l.Description.Trim(),
                    Quantity = l.Quantity,
                    Unit = string.IsNullOrWhiteSpace(l.Unit) ? "ks" : l.Unit.Trim(),
                    UnitPrice = l.UnitPrice,
                    TaxRatePercent = l.TaxRatePercent,
                    LineTotal = l.LineTotal > 0 ? l.LineTotal : l.UnitPrice * l.Quantity,
                    ProductCode = ApiStringUtil.TrimOrNull(l.ProductCode),
                    Ean = ApiStringUtil.TrimOrNull(l.Ean)
                });
            }
            return inv;
        }
    }

    private sealed record PurchaseInvoiceLineWriteDto(
        string Description,
        decimal Quantity,
        string Unit,
        decimal UnitPrice,
        decimal TaxRatePercent,
        decimal LineTotal,
        string? ProductCode,
        string? Ean);

    private sealed record PurchaseInvoiceListResponse(
        int Id,
        string Number,
        string SupplierName,
        DateTime IssueDate,
        string Status,
        string ImportSource,
        decimal TotalAmount,
        int LineCount,
        int MatchedLineCount)
    {
        public static PurchaseInvoiceListResponse FromEntity(PurchaseInvoice inv) => new(
            inv.Id,
            inv.Number,
            inv.SupplierName,
            inv.IssueDate,
            inv.Status.ToString(),
            inv.ImportSource.ToString(),
            inv.TotalAmount,
            inv.Lines.Count,
            inv.Lines.Count(l => l.FilamentTypeId is not null));
    }

    private sealed record PurchaseInvoiceLineResponse(
        int Id,
        string Description,
        decimal Quantity,
        string Unit,
        decimal UnitPrice,
        decimal TaxRatePercent,
        decimal LineTotal,
        string? ProductCode,
        string? Ean,
        int? FilamentTypeId,
        string? FilamentTypeName,
        string MatchStatus,
        int MatchConfidence,
        decimal WeightKg,
        decimal PricePerKg,
        int PieceCount)
    {
        public static PurchaseInvoiceLineResponse FromEntity(PurchaseInvoiceLine l) => new(
            l.Id,
            l.Description,
            l.Quantity,
            l.Unit,
            l.UnitPrice,
            l.TaxRatePercent,
            l.LineTotal,
            l.ProductCode,
            l.Ean,
            l.FilamentTypeId,
            l.FilamentType?.Name,
            l.MatchStatus.ToString(),
            l.MatchConfidence,
            l.WeightKg,
            l.PricePerKg,
            l.PieceCount);
    }

    private sealed record PurchaseInvoiceDetailResponse(
        int Id,
        string Number,
        DateTime IssueDate,
        DateTime? DueDate,
        string SupplierName,
        string? SupplierCompanyId,
        string? SupplierVatId,
        decimal TotalAmount,
        string Status,
        string ImportSource,
        string? SourceFileName,
        string? Notes,
        List<PurchaseInvoiceLineResponse> Lines)
    {
        public static PurchaseInvoiceDetailResponse FromEntity(PurchaseInvoice inv) => new(
            inv.Id,
            inv.Number,
            inv.IssueDate,
            inv.DueDate,
            inv.SupplierName,
            inv.SupplierCompanyId,
            inv.SupplierVatId,
            inv.TotalAmount,
            inv.Status.ToString(),
            inv.ImportSource.ToString(),
            inv.SourceFileName,
            inv.Notes,
            inv.Lines.OrderBy(l => l.Id).Select(PurchaseInvoiceLineResponse.FromEntity).ToList());
    }
}
