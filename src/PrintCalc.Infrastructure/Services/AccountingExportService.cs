using System.Text;
using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;

namespace PrintCalc.Infrastructure.Services;

/// <summary>Jednoduchý CSV export vhodný jako základ pro ABRA / Flexi / ruční import.</summary>
public class AccountingExportService : IAccountingExportService
{
    private readonly AppDbContext _db;

    public AccountingExportService(AppDbContext db) => _db = db;

    public async Task WriteInvoiceCsvAsync(Invoice invoice, Stream utf8Stream, CancellationToken ct = default)
    {
        var full = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstAsync(i => i.Id == invoice.Id, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Document;Number;IssueDate;DueDate;Customer;CompanyId;VatId;Line;Description;Qty;UnitPrice;TaxRate;LineTotal");
        var i = 0;
        foreach (var line in full.Lines.OrderBy(l => l.Id))
        {
            i++;
            sb.Append("Invoice;");
            sb.Append(Escape(full.Number)).Append(';');
            sb.Append(full.IssueDate.ToString("yyyy-MM-dd")).Append(';');
            sb.Append(full.DueDate?.ToString("yyyy-MM-dd") ?? "").Append(';');
            sb.Append(Escape(full.Customer.Name)).Append(';');
            sb.Append(Escape(full.Customer.CompanyId)).Append(';');
            sb.Append(Escape(full.Customer.VatId)).Append(';');
            sb.Append(i).Append(';');
            sb.Append(Escape(line.Description)).Append(';');
            sb.Append(line.Quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            sb.Append(line.UnitPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            sb.Append(line.TaxRatePercent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            sb.Append(line.LineTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await utf8Stream.WriteAsync(bytes, ct);
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.Replace("\"", "\"\"");
        if (t.Contains(';') || t.Contains('"') || t.Contains('\n'))
            return $"\"{t}\"";
        return t;
    }
}
