using Microsoft.EntityFrameworkCore;
using PrintCalc.Api.Services;
using PrintCalc.Api.Util;
using DocumentLineInput = PrintCalc.Api.Services.DocumentLineInput;
using InvoiceLineInput = PrintCalc.Api.Services.InvoiceLineInput;
using CreateDocumentRequest = PrintCalc.Api.Services.CreateDocumentRequest;
using CreateInvoiceRequest = PrintCalc.Api.Services.CreateInvoiceRequest;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Helpers;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Api;

public static class MapDocumentsEndpoints
{
    public static void MapDocuments(this WebApplication app)
    {
        app.MapGet("/api/quotes", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Quotes.AsNoTracking()
                .Include(q => q.Customer)
                .OrderByDescending(q => q.IssueDate)
                .Take(300)
                .ToListAsync(ct);
            return Results.Ok(list.Select(QuoteSummaryDto.FromEntity));
        });

        app.MapGet("/api/quotes/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var q = await db.Quotes.AsNoTracking()
                .Include(x => x.Lines)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return q is null ? Results.NotFound() : Results.Ok(QuoteDetailDto.FromEntity(q));
        });

        app.MapPost("/api/quotes", async (
            CreateDocumentRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.CustomerId <= 0)
                return Results.BadRequest(new { error = "Vyberte zákazníka." });
            if (!await db.Customers.AsNoTracking().AnyAsync(c => c.Id == body.CustomerId, ct))
                return Results.BadRequest(new { error = "Zákazník neexistuje." });

            var num = await numbers.NextQuoteNumberAsync(ct);
            var quote = new Quote
            {
                CustomerId = body.CustomerId,
                Number = num,
                Title = string.IsNullOrWhiteSpace(body.Title) ? "Nová nabídka" : body.Title.Trim(),
                IssueDate = DateTime.UtcNow,
                Status = QuoteStatus.Draft,
                TotalAmount = 0
            };
            DocumentFactory.ApplyDocumentLines(quote, body.Lines);
            quote.TotalAmount = quote.Lines.Sum(l => l.LineTotal);
            db.Quotes.Add(quote);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/quotes/{quote.Id}", new { quote.Id });
        });

        app.MapPost("/api/quotes/from-calculations", async (
            QuoteFromCalculationsRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.CalculationIds.Length == 0)
                return Results.BadRequest(new { error = "Vyberte alespoň jednu kalkulaci." });
            var calcs = await db.Calculations.AsNoTracking()
                .Where(c => body.CalculationIds.Contains(c.Id))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);
            if (calcs.Count == 0)
                return Results.BadRequest(new { error = "Kalkulace nenalezeny." });

            var grouped = calcs.Where(c => c.CustomerId is not null)
                .GroupBy(c => c.CustomerId!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (grouped is null)
                return Results.BadRequest(new { error = "Kalkulace musí mít přiřazeného zákazníka." });

            if (body.CustomerId is { } expectedCustomer && grouped.Key != expectedCustomer)
                return Results.BadRequest(new { error = "Vybrané kalkulace musí patřit stejnému zákazníkovi." });

            var num = await numbers.NextQuoteNumberAsync(ct);
            var items = grouped.ToList();
            var quote = new Quote
            {
                CustomerId = grouped.Key,
                Number = num,
                Title = items.Count == 1 ? items[0].Title : DocumentTitleExcerpt.FromCalculationTitles(items),
                IssueDate = DateTime.UtcNow,
                Status = QuoteStatus.Draft,
                TotalAmount = 0
            };
            foreach (var c in items)
            {
                if (body.Detailed)
                    QuoteFromCalculationHelper.AddDetailedLines(quote, c);
                else
                {
                    var label = string.IsNullOrWhiteSpace(c.Title) ? $"Kalkulace #{c.Id}" : c.Title.Trim();
                    quote.Lines.Add(new QuoteLine
                    {
                        SourceCalculationId = c.Id,
                        Description = QuoteFromCalculationHelper.BuildPrintLineDescription(c, label),
                        Quantity = 1,
                        UnitPrice = c.TotalWithMargin,
                        LineTotal = c.TotalWithMargin
                    });
                }
            }

            quote.TotalAmount = quote.Lines.Sum(l => l.LineTotal);
            db.Quotes.Add(quote);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/quotes/{quote.Id}", new { quote.Id });
        });

        app.MapPut("/api/quotes/{id:int}", async (int id, QuoteWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var q = await db.Quotes.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (q is null) return Results.NotFound();
            if (body.CustomerId is > 0 && await db.Customers.AsNoTracking().AnyAsync(c => c.Id == body.CustomerId, ct))
                q.CustomerId = body.CustomerId.Value;
            q.Title = string.IsNullOrWhiteSpace(body.Title) ? q.Title : body.Title.Trim();
            q.Status = body.Status;
            q.Notes = ApiStringUtil.TrimOrNull(body.Notes);
            db.QuoteLines.RemoveRange(q.Lines);
            q.Lines.Clear();
            foreach (var l in body.Lines)
            {
                var qty = l.Quantity <= 0 ? 1 : l.Quantity;
                var unit = l.UnitPrice < 0 ? 0 : l.UnitPrice;
                q.Lines.Add(new QuoteLine
                {
                    SourceCalculationId = l.SourceCalculationId,
                    Description = string.IsNullOrWhiteSpace(l.Description) ? "Položka" : l.Description.Trim(),
                    Quantity = qty,
                    UnitPrice = unit,
                    LineTotal = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero)
                });
            }

            q.TotalAmount = q.Lines.Sum(x => x.LineTotal);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/quotes/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var q = await db.Quotes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (q is null) return Results.NotFound();
            db.Quotes.Remove(q);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapGet("/api/quotes/{id:int}/pdf", async (int id, AppDbContext db, IQuotePdfService pdf, CancellationToken ct) =>
        {
            var q = await db.Quotes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (q is null) return Results.NotFound();
            return await PdfResultAsync(ct, async dir =>
            {
                var path = await pdf.SaveQuotePdfAsync(q, dir, ct);
                return (path, $"nabidka-{q.Number}.pdf");
            });
        });

        app.MapGet("/api/orders", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .Take(300)
                .ToListAsync(ct);
            return Results.Ok(list.Select(OrderSummaryDto.FromEntity));
        });

        app.MapGet("/api/orders/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var o = await db.Orders.AsNoTracking()
                .Include(x => x.Lines)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return o is null ? Results.NotFound() : Results.Ok(OrderDetailDto.FromEntity(o));
        });

        app.MapPost("/api/orders", async (
            CreateDocumentRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.CustomerId <= 0)
                return Results.BadRequest(new { error = "Vyberte zákazníka." });
            if (!await db.Customers.AsNoTracking().AnyAsync(c => c.Id == body.CustomerId, ct))
                return Results.BadRequest(new { error = "Zákazník neexistuje." });

            var num = await numbers.NextOrderNumberAsync(ct);
            var order = new Order
            {
                CustomerId = body.CustomerId,
                Number = num,
                Title = string.IsNullOrWhiteSpace(body.Title) ? "Nová zakázka" : body.Title.Trim(),
                Status = OrderStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = 0
            };
            DocumentFactory.ApplyDocumentLines(order, body.Lines);
            order.TotalAmount = order.Lines.Sum(l => l.LineTotal);
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{order.Id}", new { order.Id });
        });

        app.MapPost("/api/orders/from-quotes", async (
            FromQuotesOrderRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.QuoteIds.Length == 0)
                return Results.BadRequest(new { error = "Vyberte nabídky." });
            var quotes = await db.Quotes.AsNoTracking()
                .Include(q => q.Lines)
                .Where(q => body.QuoteIds.Contains(q.Id))
                .OrderBy(q => q.IssueDate)
                .ToListAsync(ct);
            if (quotes.Count == 0)
                return Results.BadRequest(new { error = "Nabídky nenalezeny." });

            var group = quotes.Where(q => q.CustomerId > 0).GroupBy(q => q.CustomerId)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            if (group is null)
                return Results.BadRequest(new { error = "Neplatný zákazník u nabídek." });

            var items = group.OrderBy(q => q.IssueDate).ToList();
            var num = await numbers.NextOrderNumberAsync(ct);
            var o = new Order
            {
                CustomerId = group.Key,
                Number = num,
                Title = items.Count == 1 ? items[0].Title : DocumentTitleExcerpt.FromQuotesForOrderTitle(items),
                QuoteId = items.Count == 1 ? items[0].Id : null,
                TotalAmount = items.Sum(x => x.TotalAmount),
                Status = OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var quote in items)
            {
                if (body.Detailed)
                {
                    foreach (var l in quote.Lines)
                    {
                        o.Lines.Add(new OrderLine
                        {
                            Description = items.Count == 1 ? l.Description : $"[{quote.Number}] {l.Description}",
                            Quantity = l.Quantity,
                            UnitPrice = l.UnitPrice,
                            LineTotal = l.LineTotal,
                            SourceCalculationId = l.SourceCalculationId
                        });
                    }
                }
                else
                {
                    o.Lines.Add(new OrderLine
                    {
                        Description = items.Count == 1 ? quote.Title : $"[{quote.Number}] {quote.Title}",
                        Quantity = 1,
                        UnitPrice = quote.TotalAmount,
                        LineTotal = quote.TotalAmount
                    });
                }
            }

            o.Title = DocumentTitleExcerpt.ForOrderGridCaption(o);
            db.Orders.Add(o);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{o.Id}", new { o.Id });
        });

        app.MapPut("/api/orders/{id:int}", async (int id, OrderWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var o = await db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (o is null) return Results.NotFound();
            if (body.CustomerId is > 0 && await db.Customers.AsNoTracking().AnyAsync(c => c.Id == body.CustomerId, ct))
                o.CustomerId = body.CustomerId.Value;
            o.Title = string.IsNullOrWhiteSpace(body.Title) ? o.Title : body.Title.Trim();
            o.Status = body.Status;
            db.OrderLines.RemoveRange(o.Lines);
            o.Lines.Clear();
            foreach (var l in body.Lines)
            {
                var qty = l.Quantity <= 0 ? 1 : l.Quantity;
                var unit = l.UnitPrice < 0 ? 0 : l.UnitPrice;
                o.Lines.Add(new OrderLine
                {
                    SourceCalculationId = l.SourceCalculationId,
                    Description = string.IsNullOrWhiteSpace(l.Description) ? "Položka" : l.Description.Trim(),
                    Quantity = qty,
                    UnitPrice = unit,
                    LineTotal = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero)
                });
            }

            o.TotalAmount = o.Lines.Sum(x => x.LineTotal);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/orders/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var o = await db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (o is null) return Results.NotFound();
            db.Orders.Remove(o);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapGet("/api/invoices", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Invoices.AsNoTracking()
                .Include(i => i.Customer)
                .OrderByDescending(i => i.IssueDate)
                .Take(400)
                .ToListAsync(ct);
            return Results.Ok(list.Select(InvoiceSummaryDto.FromEntity));
        });

        app.MapGet("/api/invoices/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var inv = await db.Invoices.AsNoTracking()
                .Include(x => x.Lines)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return inv is null ? Results.NotFound() : Results.Ok(InvoiceDetailDto.FromEntity(inv));
        });

        app.MapPost("/api/invoices", async (
            CreateInvoiceRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.CustomerId <= 0)
                return Results.BadRequest(new { error = "Vyberte zákazníka." });
            if (!await db.Customers.AsNoTracking().AnyAsync(c => c.Id == body.CustomerId, ct))
                return Results.BadRequest(new { error = "Zákazník neexistuje." });

            var vat = await AppSettingsQueries.GetDecimalAsync(db, "Finance.DefaultVatRatePercent", 21m, ct);
            var prefix = string.IsNullOrWhiteSpace(body.InvoiceNumberPrefix) ? null : body.InvoiceNumberPrefix.Trim();
            var num = await numbers.NextInvoiceNumberAsync(prefix, ct);
            var dueDays = body.DueDays > 0 ? body.DueDays : 14;
            var inv = new Invoice
            {
                CustomerId = body.CustomerId,
                Number = num,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(dueDays),
                PaymentMethod = ApiStringUtil.TrimOrNull(body.PaymentMethod) ?? "Převodem",
                Status = InvoiceStatus.Draft,
                TotalAmount = 0
            };
            DocumentFactory.ApplyInvoiceLines(inv, body.Lines, vat);
            inv.TotalAmount = inv.Lines.Sum(l => l.LineTotal);
            db.Invoices.Add(inv);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/invoices/{inv.Id}", new { inv.Id });
        });

        app.MapPost("/api/invoices/from-quotes", async (
            FromQuotesInvoiceRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.QuoteIds.Length == 0)
                return Results.BadRequest(new { error = "Vyberte nabídky." });
            var quotes = await db.Quotes.AsNoTracking()
                .Include(q => q.Lines)
                .Where(q => body.QuoteIds.Contains(q.Id))
                .OrderBy(q => q.IssueDate)
                .ToListAsync(ct);
            if (quotes.Count == 0)
                return Results.BadRequest(new { error = "Nabídky nenalezeny." });

            var group = quotes.Where(q => q.CustomerId > 0).GroupBy(q => q.CustomerId)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            if (group is null)
                return Results.BadRequest(new { error = "Neplatný zákazník u nabídek." });

            var items = group.OrderBy(q => q.IssueDate).ToList();
            var vat = await AppSettingsQueries.GetDecimalAsync(db, "Finance.DefaultVatRatePercent", 21m, ct);
            var prefix = string.IsNullOrWhiteSpace(body.InvoiceNumberPrefix) ? null : body.InvoiceNumberPrefix.Trim();
            var num = await numbers.NextInvoiceNumberAsync(prefix, ct);
            var dueDays = body.DueDays > 0 ? body.DueDays : 14;
            var inv = new Invoice
            {
                CustomerId = group.Key,
                Number = num,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(dueDays),
                PaymentMethod = ApiStringUtil.TrimOrNull(body.PaymentMethod) ?? "Převodem",
                Status = InvoiceStatus.Draft,
                TotalAmount = 0
            };
            foreach (var quote in items)
            {
                if (body.Detailed)
                {
                    var prefixLine = items.Count == 1 ? null : $"[{quote.Number}] ";
                    DocumentFactory.AddInvoiceLinesFromQuoteLines(inv, quote.Lines, vat, prefixLine);
                }
                else
                {
                    inv.Lines.Add(new InvoiceLine
                    {
                        Description = items.Count == 1 ? quote.Title : $"[{quote.Number}] {quote.Title}",
                        Quantity = 1,
                        UnitPrice = quote.TotalAmount,
                        TaxRatePercent = vat,
                        LineTotal = quote.TotalAmount
                    });
                }
            }

            inv.TotalAmount = inv.Lines.Sum(l => l.LineTotal);
            db.Invoices.Add(inv);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/invoices/{inv.Id}", new { inv.Id });
        });

        app.MapPost("/api/invoices/from-calculations", async (
            InvoiceFromCalculationsRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.CalculationIds.Length == 0)
                return Results.BadRequest(new { error = "Vyberte kalkulace." });
            var calcs = await db.Calculations.AsNoTracking()
                .Where(c => body.CalculationIds.Contains(c.Id))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);
            if (calcs.Count == 0)
                return Results.BadRequest(new { error = "Kalkulace nenalezeny." });

            var grouped = calcs.Where(c => c.CustomerId is not null)
                .GroupBy(c => c.CustomerId!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (grouped is null)
                return Results.BadRequest(new { error = "Kalkulace musí mít zákazníka." });

            var vat = await AppSettingsQueries.GetDecimalAsync(db, "Finance.DefaultVatRatePercent", 21m, ct);
            var prefix = string.IsNullOrWhiteSpace(body.InvoiceNumberPrefix) ? null : body.InvoiceNumberPrefix.Trim();
            var num = await numbers.NextInvoiceNumberAsync(prefix, ct);
            var dueDays = body.DueDays > 0 ? body.DueDays : 14;
            var inv = new Invoice
            {
                CustomerId = grouped.Key,
                Number = num,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(dueDays),
                PaymentMethod = ApiStringUtil.TrimOrNull(body.PaymentMethod) ?? "Převodem",
                Status = InvoiceStatus.Draft,
                TotalAmount = 0
            };
            var quoteLines = DocumentFactory.BuildLinesFromCalculations(grouped, body.Detailed);
            DocumentFactory.AddInvoiceLinesFromQuoteLines(inv, quoteLines, vat);
            inv.TotalAmount = inv.Lines.Sum(l => l.LineTotal);
            db.Invoices.Add(inv);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/invoices/{inv.Id}", new { inv.Id });
        });

        app.MapPost("/api/invoices/from-orders", async (
            FromOrdersInvoiceRequest body,
            AppDbContext db,
            IDocumentNumberService numbers,
            CancellationToken ct) =>
        {
            if (body.OrderIds.Length == 0)
                return Results.BadRequest(new { error = "Vyberte zakázky." });
            var orders = await db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .Where(o => body.OrderIds.Contains(o.Id))
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(ct);
            if (orders.Count == 0)
                return Results.BadRequest(new { error = "Zakázky nenalezeny." });

            var group = orders.Where(o => o.CustomerId > 0).GroupBy(o => o.CustomerId)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            if (group is null)
                return Results.BadRequest(new { error = "Neplatný zákazník." });

            var items = group.OrderBy(o => o.CreatedAt).ToList();
            var vat = await AppSettingsQueries.GetDecimalAsync(db, "Finance.DefaultVatRatePercent", 21m, ct);
            var invPrefix = string.IsNullOrWhiteSpace(body.InvoiceNumberPrefix)
                ? null
                : body.InvoiceNumberPrefix.Trim();
            var num = await numbers.NextInvoiceNumberAsync(invPrefix, ct);
            var dueDays = body.DueDays > 0 ? body.DueDays : 14;
            var inv = new Invoice
            {
                CustomerId = group.Key,
                Number = num,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(dueDays),
                PaymentMethod = ApiStringUtil.TrimOrNull(body.PaymentMethod) ?? "Převodem",
                OrderId = items.Count == 1 ? items[0].Id : null,
                Status = InvoiceStatus.Draft,
                TotalAmount = items.Sum(x => x.TotalAmount)
            };
            foreach (var order in items)
            {
                if (body.Detailed)
                {
                    foreach (var l in order.Lines)
                    {
                        inv.Lines.Add(new InvoiceLine
                        {
                            Description = l.Description,
                            Quantity = l.Quantity,
                            UnitPrice = l.UnitPrice,
                            TaxRatePercent = vat,
                            LineTotal = l.LineTotal,
                            SourceCalculationId = l.SourceCalculationId,
                            SourceOrderId = order.Id,
                            SourceOrderLineId = l.Id
                        });
                    }
                }
                else
                {
                    inv.Lines.Add(new InvoiceLine
                    {
                        Description = order.Title,
                        Quantity = 1,
                        UnitPrice = order.TotalAmount,
                        TaxRatePercent = vat,
                        LineTotal = order.TotalAmount,
                        SourceOrderId = order.Id
                    });
                }
            }

            inv.TotalAmount = inv.Lines.Sum(l => l.LineTotal);
            db.Invoices.Add(inv);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/invoices/{inv.Id}", new { inv.Id });
        });

        app.MapPatch("/api/invoices/{id:int}/mark-paid", async (
            int id,
            MarkInvoicePaidRequest? body,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (inv is null) return Results.NotFound();
            if (inv.Status == InvoiceStatus.Cancelled)
                return Results.BadRequest(new { error = "Zrušenou fakturu nelze označit jako uhrazenou." });

            var amount = body?.PaidAmount is > 0 ? body.PaidAmount.Value : inv.TotalAmount;
            inv.PaidAmount = amount;
            inv.Status = InvoiceStatus.Paid;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapPut("/api/invoices/{id:int}", async (int id, InvoiceWriteDto body, AppDbContext db, CancellationToken ct) =>
        {
            var inv = await db.Invoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (inv is null) return Results.NotFound();
            if (body.CustomerId is > 0 && await db.Customers.AsNoTracking().AnyAsync(c => c.Id == body.CustomerId, ct))
                inv.CustomerId = body.CustomerId.Value;
            inv.Status = body.Status;
            inv.PaymentMethod = ApiStringUtil.TrimOrNull(body.PaymentMethod) ?? inv.PaymentMethod;
            inv.Notes = ApiStringUtil.TrimOrNull(body.Notes);
            if (body.IssueDate.HasValue)
                inv.IssueDate = DateTime.SpecifyKind(body.IssueDate.Value, DateTimeKind.Utc);
            if (body.DueDate.HasValue)
                inv.DueDate = DateTime.SpecifyKind(body.DueDate.Value, DateTimeKind.Utc);
            db.InvoiceLines.RemoveRange(inv.Lines);
            inv.Lines.Clear();
            foreach (var l in body.Lines)
            {
                var qty = l.Quantity <= 0 ? 1 : l.Quantity;
                var unit = l.UnitPrice < 0 ? 0 : l.UnitPrice;
                var tax = l.TaxRatePercent < 0 ? 0 : l.TaxRatePercent;
                inv.Lines.Add(new InvoiceLine
                {
                    SourceCalculationId = l.SourceCalculationId,
                    SourceOrderId = l.SourceOrderId,
                    SourceOrderLineId = l.SourceOrderLineId,
                    Description = string.IsNullOrWhiteSpace(l.Description) ? "Položka" : l.Description.Trim(),
                    Quantity = qty,
                    UnitPrice = unit,
                    TaxRatePercent = tax,
                    LineTotal = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero)
                });
            }

            inv.TotalAmount = inv.Lines.Sum(x => x.LineTotal);
            inv.PaidAmount = body.PaidAmount < 0 ? 0 : body.PaidAmount;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapDelete("/api/invoices/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (inv is null) return Results.NotFound();
            db.Invoices.Remove(inv);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapGet("/api/invoices/{id:int}/pdf", async (int id, AppDbContext db, IInvoicePdfService pdf, CancellationToken ct) =>
        {
            var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (inv is null) return Results.NotFound();
            return await PdfResultAsync(ct, async dir =>
            {
                var path = await pdf.SaveInvoicePdfAsync(inv, dir, ct);
                return (path, $"faktura-{inv.Number}.pdf");
            });
        });

        app.MapGet("/api/invoices/{id:int}/csv", async (int id, AppDbContext db, IAccountingExportService csv, CancellationToken ct) =>
        {
            var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (inv is null) return Results.NotFound();
            var ms = new MemoryStream();
            await csv.WriteInvoiceCsvAsync(inv, ms, ct);
            ms.Position = 0;
            return Results.File(ms.ToArray(), "text/csv; charset=utf-8", $"faktura-{inv.Number}.csv");
        });

        app.MapGet("/api/calculations/{id:int}/pdf", async (int id, AppDbContext db, ICalculationPdfService pdf, CancellationToken ct) =>
        {
            var calc = await db.Calculations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (calc is null) return Results.NotFound();
            return await PdfResultAsync(ct, async dir =>
            {
                var path = await pdf.SaveCalculationPdfAsync(calc, dir, ct);
                return (path, $"kalkulace-{calc.Id}.pdf");
            });
        });
    }

    private static async Task<IResult> PdfResultAsync(
        CancellationToken ct,
        Func<string, Task<(string path, string downloadName)>> save)
    {
        var dir = Path.Combine(Path.GetTempPath(), "printcalc-api-pdf");
        Directory.CreateDirectory(dir);
        var (path, downloadName) = await save(dir);
        var bytes = await File.ReadAllBytesAsync(path, ct);
        try
        {
            File.Delete(path);
        }
        catch
        {
            /* ignore */
        }

        return Results.File(bytes, "application/pdf", downloadName);
    }
}

public record QuoteFromCalculationsRequest(
    int[] CalculationIds,
    bool Detailed,
    int? CustomerId);

public record FromQuotesOrderRequest(int[] QuoteIds, bool Detailed);

public record FromOrdersInvoiceRequest(
    int[] OrderIds,
    bool Detailed,
    int DueDays,
    string? PaymentMethod,
    string? InvoiceNumberPrefix);

public record FromQuotesInvoiceRequest(
    int[] QuoteIds,
    bool Detailed,
    int DueDays,
    string? PaymentMethod,
    string? InvoiceNumberPrefix);

public record InvoiceFromCalculationsRequest(
    int[] CalculationIds,
    bool Detailed,
    int DueDays,
    string? PaymentMethod,
    string? InvoiceNumberPrefix);

public record DocumentLineWriteDto(
    int? SourceCalculationId,
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public record InvoiceLineWriteDto(
    int? SourceCalculationId,
    int? SourceOrderId,
    int? SourceOrderLineId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePercent);

public record QuoteWriteDto(
    int? CustomerId,
    string Title,
    QuoteStatus Status,
    string? Notes,
    IReadOnlyList<DocumentLineWriteDto> Lines);

public record OrderWriteDto(
    int? CustomerId,
    string Title,
    OrderStatus Status,
    IReadOnlyList<DocumentLineWriteDto> Lines);

public record MarkInvoicePaidRequest(decimal? PaidAmount);

public record InvoiceWriteDto(
    int? CustomerId,
    InvoiceStatus Status,
    string? PaymentMethod,
    string? Notes,
    DateTime? IssueDate,
    DateTime? DueDate,
    decimal PaidAmount,
    IReadOnlyList<InvoiceLineWriteDto> Lines);

public record QuoteSummaryDto(int Id, string Number, string Title, int CustomerId, string CustomerName, string Status, decimal TotalAmount, DateTime IssueDate)
{
    public static QuoteSummaryDto FromEntity(Quote q) => new(
        q.Id,
        q.Number,
        q.Title,
        q.CustomerId,
        q.Customer.Name,
        q.Status.ToString(),
        q.TotalAmount,
        q.IssueDate);
}

public record QuoteLineDto(int Id, int? SourceCalculationId, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record QuoteDetailDto(
    int Id,
    string Number,
    string Title,
    int CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTime IssueDate,
    string? Notes,
    int? SourceCalculationId,
    IReadOnlyList<QuoteLineDto> Lines)
{
    public static QuoteDetailDto FromEntity(Quote q) => new(
        q.Id,
        q.Number,
        q.Title,
        q.CustomerId,
        q.Customer.Name,
        q.Status.ToString(),
        q.TotalAmount,
        q.IssueDate,
        q.Notes,
        q.SourceCalculationId,
        q.Lines.OrderBy(l => l.Id).Select(l => new QuoteLineDto(l.Id, l.SourceCalculationId, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}

public record OrderSummaryDto(int Id, string Number, string Title, int CustomerId, string CustomerName, string Status, decimal TotalAmount, DateTime CreatedAt)
{
    public static OrderSummaryDto FromEntity(Order o) => new(
        o.Id,
        o.Number,
        o.Title,
        o.CustomerId,
        o.Customer.Name,
        o.Status.ToString(),
        o.TotalAmount,
        o.CreatedAt);
}

public record OrderLineDto(int Id, int? SourceCalculationId, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDetailDto(
    int Id,
    string Number,
    string Title,
    int CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    int? QuoteId,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDetailDto FromEntity(Order o) => new(
        o.Id,
        o.Number,
        o.Title,
        o.CustomerId,
        o.Customer.Name,
        o.Status.ToString(),
        o.TotalAmount,
        o.CreatedAt,
        o.QuoteId,
        o.Lines.OrderBy(l => l.Id).Select(l => new OrderLineDto(l.Id, l.SourceCalculationId, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}

public record InvoiceSummaryDto(int Id, string Number, int CustomerId, string CustomerName, string Status, decimal TotalAmount, DateTime IssueDate)
{
    public static InvoiceSummaryDto FromEntity(Invoice i) => new(
        i.Id,
        i.Number,
        i.CustomerId,
        i.Customer.Name,
        i.Status.ToString(),
        i.TotalAmount,
        i.IssueDate);
}

public record InvoiceLineDto(
    int Id,
    int? SourceCalculationId,
    int? SourceOrderId,
    int? SourceOrderLineId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePercent,
    decimal LineTotal);

public record InvoiceDetailDto(
    int Id,
    string Number,
    int CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    DateTime IssueDate,
    DateTime? DueDate,
    string? PaymentMethod,
    string? Notes,
    int? OrderId,
    IReadOnlyList<InvoiceLineDto> Lines)
{
    public static InvoiceDetailDto FromEntity(Invoice i) => new(
        i.Id,
        i.Number,
        i.CustomerId,
        i.Customer.Name,
        i.Status.ToString(),
        i.TotalAmount,
        i.PaidAmount,
        i.IssueDate,
        i.DueDate,
        i.PaymentMethod,
        i.Notes,
        i.OrderId,
        i.Lines.OrderBy(l => l.Id).Select(l => new InvoiceLineDto(
            l.Id,
            l.SourceCalculationId,
            l.SourceOrderId,
            l.SourceOrderLineId,
            l.Description,
            l.Quantity,
            l.UnitPrice,
            l.TaxRatePercent,
            l.LineTotal)).ToList());
}
