using PrintCalc.Core.Entities;
using PrintCalc.Core.Enums;
using PrintCalc.Core.Helpers;

namespace PrintCalc.Api.Services;

internal static class DocumentFactory
{
    public static void AddInvoiceLinesFromQuoteLines(
        Invoice inv,
        IEnumerable<QuoteLine> lines,
        decimal vatPercent,
        string? linePrefix = null)
    {
        foreach (var l in lines)
        {
            var desc = string.IsNullOrWhiteSpace(linePrefix)
                ? l.Description
                : $"{linePrefix}{l.Description}";
            inv.Lines.Add(new InvoiceLine
            {
                Description = desc,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRatePercent = vatPercent,
                LineTotal = l.LineTotal,
                SourceCalculationId = l.SourceCalculationId
            });
        }
    }

    public static void AddInvoiceLinesFromOrderLines(
        Invoice inv,
        Order order,
        IEnumerable<OrderLine> lines,
        decimal vatPercent,
        bool detailed)
    {
        if (detailed)
        {
            foreach (var l in lines)
            {
                inv.Lines.Add(new InvoiceLine
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TaxRatePercent = vatPercent,
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
                TaxRatePercent = vatPercent,
                LineTotal = order.TotalAmount,
                SourceOrderId = order.Id
            });
        }
    }

    public static List<QuoteLine> BuildLinesFromCalculations(
        IEnumerable<Calculation> calculations,
        bool detailed)
    {
        var lines = new List<QuoteLine>();
        foreach (var c in calculations)
        {
            if (detailed)
            {
                var q = new Quote { Lines = lines };
                QuoteFromCalculationHelper.AddDetailedLines(q, c);
            }
            else
            {
                var label = string.IsNullOrWhiteSpace(c.Title) ? $"Kalkulace #{c.Id}" : c.Title.Trim();
                lines.Add(new QuoteLine
                {
                    SourceCalculationId = c.Id,
                    Description = QuoteFromCalculationHelper.BuildPrintLineDescription(c, label),
                    Quantity = 1,
                    UnitPrice = c.TotalWithMargin,
                    LineTotal = c.TotalWithMargin
                });
            }
        }

        return lines;
    }

    public static void ApplyDocumentLines(
        Quote quote,
        IReadOnlyList<DocumentLineInput>? lines)
    {
        if (lines is null || lines.Count == 0) return;
        foreach (var l in lines)
        {
            var qty = l.Quantity <= 0 ? 1 : l.Quantity;
            var unit = l.UnitPrice < 0 ? 0 : l.UnitPrice;
            quote.Lines.Add(new QuoteLine
            {
                SourceCalculationId = l.SourceCalculationId,
                Description = string.IsNullOrWhiteSpace(l.Description) ? "Položka" : l.Description.Trim(),
                Quantity = qty,
                UnitPrice = unit,
                LineTotal = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero)
            });
        }
    }

    public static void ApplyDocumentLines(Order order, IReadOnlyList<DocumentLineInput>? lines)
    {
        if (lines is null || lines.Count == 0) return;
        foreach (var l in lines)
        {
            var qty = l.Quantity <= 0 ? 1 : l.Quantity;
            var unit = l.UnitPrice < 0 ? 0 : l.UnitPrice;
            order.Lines.Add(new OrderLine
            {
                SourceCalculationId = l.SourceCalculationId,
                Description = string.IsNullOrWhiteSpace(l.Description) ? "Položka" : l.Description.Trim(),
                Quantity = qty,
                UnitPrice = unit,
                LineTotal = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero)
            });
        }
    }

    public static void ApplyInvoiceLines(
        Invoice inv,
        IReadOnlyList<InvoiceLineInput>? lines,
        decimal defaultVat)
    {
        if (lines is null || lines.Count == 0) return;
        foreach (var l in lines)
        {
            var qty = l.Quantity <= 0 ? 1 : l.Quantity;
            var unit = l.UnitPrice < 0 ? 0 : l.UnitPrice;
            var tax = l.TaxRatePercent < 0 ? defaultVat : l.TaxRatePercent;
            inv.Lines.Add(new InvoiceLine
            {
                SourceCalculationId = l.SourceCalculationId,
                Description = string.IsNullOrWhiteSpace(l.Description) ? "Položka" : l.Description.Trim(),
                Quantity = qty,
                UnitPrice = unit,
                TaxRatePercent = tax,
                LineTotal = Math.Round(qty * unit, 0, MidpointRounding.AwayFromZero)
            });
        }
    }
}

public record DocumentLineInput(
    int? SourceCalculationId,
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public record InvoiceLineInput(
    int? SourceCalculationId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePercent);

public record CreateDocumentRequest(
    int CustomerId,
    string? Title,
    IReadOnlyList<DocumentLineInput>? Lines);

public record CreateInvoiceRequest(
    int CustomerId,
    int DueDays,
    string? PaymentMethod,
    string? InvoiceNumberPrefix,
    IReadOnlyList<InvoiceLineInput>? Lines);
