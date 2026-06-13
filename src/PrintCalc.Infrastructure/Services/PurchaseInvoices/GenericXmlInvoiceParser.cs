using System.Globalization;
using System.Xml.Linq;
using PrintCalc.Core.Models;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public static class GenericXmlInvoiceParser
{
    public static bool TryParse(Stream stream, out ParsedPurchaseInvoice result)
    {
        result = new ParsedPurchaseInvoice();
        try
        {
            var doc = XDocument.Load(stream);
            var root = doc.Root;
            if (root is null) return false;

            result.Number = FirstValue(root, "ID", "InvoiceNumber", "DocumentNumber", "Number") ?? "";
            result.IssueDate = ParseDate(FirstValue(root, "IssueDate", "Date", "DatumVystaveni")) ?? DateTime.UtcNow;
            result.DueDate = ParseDate(FirstValue(root, "DueDate", "PaymentDueDate", "DatumSplatnosti"));
            result.SupplierName = FirstValue(root, "SupplierName", "Dodavatel", "SellerName") ?? "";
            result.SupplierCompanyId = FirstValue(root, "SupplierIco", "SupplierCompanyId", "ICO");
            result.SupplierVatId = FirstValue(root, "SupplierDic", "SupplierVatId", "DIC");

            var lineNodes = root.Descendants().Where(e =>
                e.Name.LocalName.Contains("Line", StringComparison.OrdinalIgnoreCase)
                || e.Name.LocalName.Equals("Polozka", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var ln in lineNodes)
            {
                var desc = FirstValue(ln, "Description", "Name", "Popis", "Text") ?? "";
                if (string.IsNullOrWhiteSpace(desc)) continue;
                var qty = ParseDec(FirstValue(ln, "Quantity", "Qty", "Mnozstvi", "InvoicedQuantity")) ?? 1m;
                var unit = FirstValue(ln, "Unit", "UnitCode", "Jednotka") ?? "ks";
                var unitPrice = ParseDec(FirstValue(ln, "UnitPrice", "Cena", "Price")) ?? 0m;
                var lineTotal = ParseDec(FirstValue(ln, "LineTotal", "LineExtensionAmount", "Castka")) ?? unitPrice * qty;
                var vat = ParseDec(FirstValue(ln, "TaxRate", "VatRate", "DPH")) ?? 21m;

                result.Lines.Add(new ParsedPurchaseInvoiceLine
                {
                    Description = desc,
                    Quantity = qty,
                    Unit = unit,
                    UnitPrice = unitPrice,
                    TaxRatePercent = vat,
                    LineTotal = lineTotal,
                    ProductCode = FirstValue(ln, "ProductCode", "Code", "Kod"),
                    Ean = FirstValue(ln, "EAN", "Ean", "Barcode")
                });
            }

            result.TotalAmount = ParseDec(FirstValue(root, "TotalAmount", "PayableAmount", "TaxInclusiveAmount", "Celkem"))
                                 ?? result.Lines.Sum(l => l.LineTotal);

            return result.Lines.Count > 0 || !string.IsNullOrWhiteSpace(result.Number);
        }
        catch
        {
            return false;
        }
    }

    private static string? FirstValue(XElement root, params string[] localNames)
    {
        foreach (var name in localNames)
        {
            var el = root.DescendantsAndSelf().FirstOrDefault(e =>
                e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (el is not null && !string.IsNullOrWhiteSpace(el.Value))
                return el.Value.Trim();
        }
        return null;
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, out var d)) return d;
        return null;
    }

    private static decimal? ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
