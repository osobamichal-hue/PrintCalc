using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Enums;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Api;

public static class MapStatisticsEndpoints
{
    public static void MapStatistics(this WebApplication app)
    {
        app.MapGet("/api/statistics/dashboard", async (int? months, AppDbContext db, CancellationToken ct) =>
        {
            var periodMonths = months is > 0 and <= 36 ? months.Value : 12;
            var periodStart = DateTime.UtcNow.AddMonths(-periodMonths);

            var calcs = await db.Calculations.AsNoTracking()
                .Where(c => c.CreatedAt >= periodStart)
                .Select(c => new
                {
                    c.CreatedAt,
                    c.CustomerId,
                    c.PrinterId,
                    c.Subtotal,
                    c.TotalWithMargin,
                    c.MarginPercent,
                    c.MaterialCost,
                    c.PrintCost,
                    c.EnergyCost,
                    c.ModelDesignCost,
                    c.StartFeeCost,
                    c.PrintHours,
                    c.PrintRuns,
                })
                .ToListAsync(ct);

            var quotes = await db.Quotes.AsNoTracking()
                .Include(q => q.Customer)
                .Where(q => q.IssueDate >= periodStart)
                .Select(q => new { q.IssueDate, q.TotalAmount, q.Status, q.CustomerId, q.Customer.Name, q.Number, q.Title })
                .ToListAsync(ct);

            var orders = await db.Orders.AsNoTracking()
                .Where(o => o.CreatedAt >= periodStart)
                .Select(o => new { o.CreatedAt, o.TotalAmount, o.Status, o.CustomerId })
                .ToListAsync(ct);

            var invoices = await db.Invoices.AsNoTracking()
                .Include(i => i.Lines)
                .Include(i => i.Customer)
                .Where(i => i.IssueDate >= periodStart)
                .ToListAsync(ct);

            var calcLookup = await db.Calculations.AsNoTracking()
                .Select(c => new { c.Id, c.Subtotal, c.TotalWithMargin })
                .ToDictionaryAsync(c => c.Id, ct);

            var printers = await db.Printers.AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            var customers = await db.Customers.AsNoTracking()
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

            // --- Overview KPIs ---
            var calcCost = calcs.Sum(c => c.Subtotal);
            var calcRevenue = calcs.Sum(c => c.TotalWithMargin);
            var calcProfit = calcRevenue - calcCost;
            var avgMargin = calcs.Count > 0 ? calcs.Average(c => (double)c.MarginPercent) : 0;

            var invoicedTotal = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).Sum(i => i.TotalAmount);
            var paidTotal = invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.PaidAmount > 0 ? i.PaidAmount : i.TotalAmount);

            decimal invoicedCost = 0;
            decimal invoicedProfit = 0;
            foreach (var inv in invoices.Where(i => i.Status != InvoiceStatus.Cancelled))
            {
                foreach (var line in inv.Lines)
                {
                    if (line.SourceCalculationId is { } calcId && calcLookup.TryGetValue(calcId, out var calc))
                    {
                        var ratio = calc.TotalWithMargin > 0 ? line.LineTotal / calc.TotalWithMargin : 1;
                        var lineCost = calc.Subtotal * ratio;
                        invoicedCost += lineCost;
                        invoicedProfit += line.LineTotal - lineCost;
                    }
                    else
                    {
                        invoicedProfit += line.LineTotal;
                    }
                }
            }

            var overview = new StatisticsOverviewDto(
                QuotesCount: quotes.Count,
                QuotesValue: quotes.Sum(q => q.TotalAmount),
                OrdersCount: orders.Count,
                OrdersValue: orders.Sum(o => o.TotalAmount),
                InvoicesCount: invoices.Count(i => i.Status != InvoiceStatus.Cancelled),
                InvoicedValue: invoicedTotal,
                PaidValue: paidTotal,
                CalculationsCount: calcs.Count,
                CalculationsCost: calcCost,
                CalculationsRevenue: calcRevenue,
                CalculationsProfit: calcProfit,
                AverageMarginPercent: Math.Round((decimal)avgMargin, 1),
                InvoicedEstimatedProfit: invoicedProfit,
                InvoicedEstimatedCost: invoicedCost,
                TotalPrintHours: calcs.Sum(c => c.PrintHours * c.PrintRuns)
            );

            // --- Monthly trend ---
            var monthKeys = Enumerable.Range(0, periodMonths)
                .Select(i => DateTime.UtcNow.AddMonths(-periodMonths + 1 + i))
                .Select(d => new { d.Year, d.Month })
                .ToList();

            var monthly = monthKeys.Select(m =>
            {
                var qVal = quotes.Where(q => q.IssueDate.Year == m.Year && q.IssueDate.Month == m.Month).Sum(q => q.TotalAmount);
                var oVal = orders.Where(o => o.CreatedAt.Year == m.Year && o.CreatedAt.Month == m.Month).Sum(o => o.TotalAmount);
                var iVal = invoices.Where(i => i.IssueDate.Year == m.Year && i.IssueDate.Month == m.Month && i.Status != InvoiceStatus.Cancelled).Sum(i => i.TotalAmount);
                var paid = invoices.Where(i => i.IssueDate.Year == m.Year && i.IssueDate.Month == m.Month && i.Status == InvoiceStatus.Paid)
                    .Sum(i => i.PaidAmount > 0 ? i.PaidAmount : i.TotalAmount);
                var cost = calcs.Where(c => c.CreatedAt.Year == m.Year && c.CreatedAt.Month == m.Month).Sum(c => c.Subtotal);
                var revenue = calcs.Where(c => c.CreatedAt.Year == m.Year && c.CreatedAt.Month == m.Month).Sum(c => c.TotalWithMargin);
                return new MonthlyTrendDto(
                    $"{m.Month:D2}/{m.Year}",
                    qVal,
                    oVal,
                    iVal,
                    paid,
                    cost,
                    revenue,
                    revenue - cost
                );
            }).ToList();

            // --- Cost breakdown ---
            var breakdown = new CostBreakdownDto(
                calcs.Sum(c => c.MaterialCost),
                calcs.Sum(c => c.PrintCost),
                calcs.Sum(c => c.EnergyCost),
                calcs.Sum(c => c.ModelDesignCost),
                calcs.Sum(c => c.StartFeeCost),
                calcProfit
            );

            // --- Pipeline funnel ---
            var pipeline = new PipelineComparisonDto(
                quotes.Sum(q => q.TotalAmount),
                orders.Sum(o => o.TotalAmount),
                invoicedTotal,
                paidTotal,
                quotes.Count > 0 ? Math.Round(100m * quotes.Count(q => q.Status == QuoteStatus.Accepted) / quotes.Count, 1) : 0,
                orders.Count > 0 ? Math.Round(100m * orders.Count(o => o.Status == OrderStatus.Completed) / orders.Count, 1) : 0,
                invoices.Count(i => i.Status != InvoiceStatus.Cancelled) > 0
                    ? Math.Round(100m * invoices.Count(i => i.Status == InvoiceStatus.Paid) / invoices.Count(i => i.Status != InvoiceStatus.Cancelled), 1)
                    : 0
            );

            // --- Top customers ---
            var topCustomers = invoices
                .Where(i => i.Status != InvoiceStatus.Cancelled)
                .GroupBy(i => i.CustomerId)
                .Select(g => new CustomerStatDto(
                    g.Key,
                    customers.GetValueOrDefault(g.Key, $"#{g.Key}"),
                    g.Sum(i => i.TotalAmount),
                    g.Count(),
                    quotes.Where(q => q.CustomerId == g.Key).Sum(q => q.TotalAmount)
                ))
                .OrderByDescending(c => c.InvoicedValue)
                .Take(8)
                .ToList();

            // --- Printer utilization ---
            var printerStats = calcs
                .Where(c => c.PrinterId is not null)
                .GroupBy(c => c.PrinterId!.Value)
                .Select(g => new PrinterStatDto(
                    g.Key,
                    printers.GetValueOrDefault(g.Key, $"#{g.Key}"),
                    g.Sum(c => c.PrintHours * c.PrintRuns),
                    g.Sum(c => c.TotalWithMargin),
                    g.Count()
                ))
                .OrderByDescending(p => p.PrintHours)
                .ToList();

            // --- Status distributions ---
            var quotesByStatus = Enum.GetValues<QuoteStatus>()
                .Select(s => new StatusCountDto(s.ToString(), quotes.Count(q => q.Status == s)))
                .Where(x => x.Count > 0)
                .ToList();

            var ordersByStatus = Enum.GetValues<OrderStatus>()
                .Select(s => new StatusCountDto(s.ToString(), orders.Count(o => o.Status == s)))
                .Where(x => x.Count > 0)
                .ToList();

            var invoicesByStatus = Enum.GetValues<InvoiceStatus>()
                .Select(s => new StatusCountDto(s.ToString(), invoices.Count(i => i.Status == s)))
                .Where(x => x.Count > 0)
                .ToList();

            // --- Quote vs invoice per customer (gap analysis) ---
            var activeCustomerIds = quotes.Select(q => q.CustomerId)
                .Concat(orders.Select(o => o.CustomerId))
                .Concat(invoices.Select(i => i.CustomerId))
                .Distinct();

            var openInvoices = await db.Invoices.AsNoTracking()
                .Include(i => i.Customer)
                .Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
                .OrderBy(i => i.DueDate ?? i.IssueDate)
                .Select(i => new OpenInvoiceDto(
                    i.Id,
                    i.Number,
                    i.Customer.Name,
                    i.TotalAmount,
                    i.PaidAmount,
                    i.Status.ToString(),
                    i.DueDate,
                    i.IssueDate))
                .Take(50)
                .ToListAsync(ct);

            var quoteVsInvoice = activeCustomerIds
                .Select(cid =>
                {
                    var qSum = quotes.Where(q => q.CustomerId == cid).Sum(q => q.TotalAmount);
                    var iSum = invoices.Where(i => i.CustomerId == cid && i.Status != InvoiceStatus.Cancelled).Sum(i => i.TotalAmount);
                    if (qSum == 0 && iSum == 0) return null;
                    return new QuoteInvoiceGapDto(
                        cid,
                        customers.GetValueOrDefault(cid, $"#{cid}"),
                        qSum,
                        iSum,
                        iSum - qSum
                    );
                })
                .Where(x => x is not null)
                .Cast<QuoteInvoiceGapDto>()
                .OrderByDescending(x => Math.Abs(x.Gap))
                .Take(10)
                .ToList();

            return Results.Ok(new StatisticsDashboardDto(
                periodMonths,
                overview,
                monthly,
                breakdown,
                pipeline,
                topCustomers,
                printerStats,
                quotesByStatus,
                ordersByStatus,
                invoicesByStatus,
                quoteVsInvoice,
                openInvoices,
                openInvoices.Sum(i => i.TotalAmount - i.PaidAmount)
            ));
        });
    }
}

public record StatisticsOverviewDto(
    int QuotesCount,
    decimal QuotesValue,
    int OrdersCount,
    decimal OrdersValue,
    int InvoicesCount,
    decimal InvoicedValue,
    decimal PaidValue,
    int CalculationsCount,
    decimal CalculationsCost,
    decimal CalculationsRevenue,
    decimal CalculationsProfit,
    decimal AverageMarginPercent,
    decimal InvoicedEstimatedProfit,
    decimal InvoicedEstimatedCost,
    decimal TotalPrintHours);

public record MonthlyTrendDto(
    string Label,
    decimal Quotes,
    decimal Orders,
    decimal Invoices,
    decimal Paid,
    decimal Costs,
    decimal Revenue,
    decimal Profit);

public record CostBreakdownDto(
    decimal Material,
    decimal Print,
    decimal Energy,
    decimal ModelDesign,
    decimal StartFee,
    decimal Margin);

public record PipelineComparisonDto(
    decimal QuotesTotal,
    decimal OrdersTotal,
    decimal InvoicesTotal,
    decimal PaidTotal,
    decimal QuoteAcceptancePercent,
    decimal OrderCompletionPercent,
    decimal InvoicePaidPercent);

public record CustomerStatDto(int CustomerId, string CustomerName, decimal InvoicedValue, int InvoiceCount, decimal QuotedValue);

public record PrinterStatDto(int PrinterId, string PrinterName, decimal PrintHours, decimal Revenue, int CalculationCount);

public record StatusCountDto(string Status, int Count);

public record QuoteInvoiceGapDto(int CustomerId, string CustomerName, decimal QuotedValue, decimal InvoicedValue, decimal Gap);

public record OpenInvoiceDto(
    int Id,
    string Number,
    string CustomerName,
    decimal TotalAmount,
    decimal PaidAmount,
    string Status,
    DateTime? DueDate,
    DateTime IssueDate);

public record StatisticsDashboardDto(
    int PeriodMonths,
    StatisticsOverviewDto Overview,
    IReadOnlyList<MonthlyTrendDto> MonthlyTrend,
    CostBreakdownDto CostBreakdown,
    PipelineComparisonDto Pipeline,
    IReadOnlyList<CustomerStatDto> TopCustomers,
    IReadOnlyList<PrinterStatDto> PrinterUtilization,
    IReadOnlyList<StatusCountDto> QuotesByStatus,
    IReadOnlyList<StatusCountDto> OrdersByStatus,
    IReadOnlyList<StatusCountDto> InvoicesByStatus,
    IReadOnlyList<QuoteInvoiceGapDto> QuoteInvoiceGap,
    IReadOnlyList<OpenInvoiceDto> OpenInvoices,
    decimal OpenInvoicesRemaining);
