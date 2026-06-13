using Microsoft.EntityFrameworkCore;
using PrintCalc.Api.Contracts;
using PrintCalc.Api.Util;
using PrintCalc.Core.Entities;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Api;

public static class MapCoreEndpoints
{
    public static void MapCore(this WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "PrintCalc.Api" }));

        app.MapGet("/api/settings", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.AppSettings.AsNoTracking().OrderBy(x => x.Key).ToListAsync(ct);
            return Results.Ok(rows.Select(x => new AppSettingDto(x.Key, x.Value)));
        });

        app.MapPut("/api/settings", async (IReadOnlyList<AppSettingDto> body, AppDbContext db, CancellationToken ct) =>
        {
            foreach (var row in body)
            {
                if (string.IsNullOrWhiteSpace(row.Key)) continue;
                var tracked = await db.AppSettings.FirstOrDefaultAsync(x => x.Key == row.Key, ct);
                if (tracked is null)
                    db.AppSettings.Add(new AppSettingsRow { Key = row.Key.Trim(), Value = row.Value ?? "" });
                else
                    tracked.Value = row.Value ?? "";
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapGet("/api/lookups", async (AppDbContext db, CancellationToken ct) =>
        {
            var customers = await db.Customers.AsNoTracking().OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name }).ToListAsync(ct);
            var filamentTypes = await db.FilamentTypes.AsNoTracking().OrderBy(f => f.Name)
                .Select(f => new
                {
                    f.Id,
                    f.Name,
                    f.Manufacturer,
                    f.AveragePricePerKg,
                    f.DiameterMm,
                    f.Color
                }).ToListAsync(ct);
            var printers = await db.Printers.AsNoTracking().OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name, kind = p.Kind.ToString(), p.HourlyRate, p.KwhPerHour, p.StartFeePerPrint })
                .ToListAsync(ct);
            var models = await db.PrintModels.AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(300)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.FileType,
                    m.OriginalFileName,
                    m.EstimatedMaterialGrams,
                    m.EstimatedPrintHours,
                    m.CreatedAt
                }).ToListAsync(ct);
            return Results.Ok(new { customers, filamentTypes, printers, printModels = models });
        });

        app.MapGet("/api/customers", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Customers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CustomerDto(
                    c.Id,
                    c.Name,
                    c.CompanyId,
                    c.VatId,
                    c.Street,
                    c.City,
                    c.Zip,
                    c.Email,
                    c.Phone,
                    c.InvoiceDueDays,
                    c.PreferredPaymentMethod,
                    c.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        app.MapGet("/api/customers/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return Results.NotFound();
            return Results.Ok(new CustomerDto(
                c.Id,
                c.Name,
                c.CompanyId,
                c.VatId,
                c.Street,
                c.City,
                c.Zip,
                c.Email,
                c.Phone,
                c.InvoiceDueDays,
                c.PreferredPaymentMethod,
                c.CreatedAt));
        });

        app.MapPost("/api/customers", async (CustomerWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Vyplňte název zákazníka." });

            var c = new Customer
            {
                Name = body.Name.Trim(),
                CompanyId = ApiStringUtil.TrimOrNull(body.CompanyId),
                VatId = ApiStringUtil.TrimOrNull(body.VatId),
                Street = ApiStringUtil.TrimOrNull(body.Street),
                City = ApiStringUtil.TrimOrNull(body.City),
                Zip = ApiStringUtil.TrimOrNull(body.Zip),
                Email = ApiStringUtil.TrimOrNull(body.Email),
                Phone = ApiStringUtil.TrimOrNull(body.Phone),
                InvoiceDueDays = body.InvoiceDueDays is > 0 ? body.InvoiceDueDays : null,
                PreferredPaymentMethod = ApiStringUtil.TrimOrNull(body.PreferredPaymentMethod)
            };
            db.Customers.Add(c);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{c.Id}", new CustomerDto(
                c.Id,
                c.Name,
                c.CompanyId,
                c.VatId,
                c.Street,
                c.City,
                c.Zip,
                c.Email,
                c.Phone,
                c.InvoiceDueDays,
                c.PreferredPaymentMethod,
                c.CreatedAt));
        });

        app.MapPut("/api/customers/{id:int}", async (int id, CustomerWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "Vyplňte název zákazníka." });

            c.Name = body.Name.Trim();
            c.CompanyId = ApiStringUtil.TrimOrNull(body.CompanyId);
            c.VatId = ApiStringUtil.TrimOrNull(body.VatId);
            c.Street = ApiStringUtil.TrimOrNull(body.Street);
            c.City = ApiStringUtil.TrimOrNull(body.City);
            c.Zip = ApiStringUtil.TrimOrNull(body.Zip);
            c.Email = ApiStringUtil.TrimOrNull(body.Email);
            c.Phone = ApiStringUtil.TrimOrNull(body.Phone);
            c.InvoiceDueDays = body.InvoiceDueDays is > 0 ? body.InvoiceDueDays : null;
            c.PreferredPaymentMethod = ApiStringUtil.TrimOrNull(body.PreferredPaymentMethod);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/customers/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return Results.NotFound();
            db.Customers.Remove(c);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
