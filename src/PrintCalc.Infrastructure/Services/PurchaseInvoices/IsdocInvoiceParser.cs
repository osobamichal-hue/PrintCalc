using System.Globalization;
using System.Xml.Linq;
using PrintCalc.Core.Models;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public static class IsdocInvoiceParser
{
    private static readonly XNamespace IsdocNs = "http://isdoc.cz/namespace/2013";

    public static bool TryParse(Stream stream, out ParsedPurchaseInvoice result)
    {
        result = new ParsedPurchaseInvoice();
        try
        {
            var doc = XDocument.Load(stream);
            var root = doc.Root;
            if (root is null) return false;

            var isIsdoc = root.Name.Namespace == IsdocNs
                          || root.Descendants().Any(e => e.Name.Namespace == IsdocNs);
            if (!isIsdoc) return false;

            XElement? Find(string localName) =>
                root.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase)
                    && (e.Name.Namespace == IsdocNs || e.Name.Namespace == XNamespace.None));

            result.Number = Find("ID")?.Value.Trim() ?? "";
            result.IssueDate = ParseDate(Find("IssueDate")?.Value) ?? DateTime.UtcNow;
            result.DueDate = ParseDate(Find("PaymentDueDate")?.Value ?? Find("TaxPointDate")?.Value);

            var supplierParty = root.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.Equals("AccountingSupplierParty", StringComparison.OrdinalIgnoreCase));
            if (supplierParty is not null)
            {
                result.SupplierName = supplierParty.Descendants().FirstOrDefault(e => e.Name.LocalName == "Name")?.Value.Trim() ?? "";
                result.SupplierCompanyId = supplierParty.Descendants().FirstOrDefault(e => e.Name.LocalName == "CompanyID")?.Value.Trim();
                result.SupplierVatId = supplierParty.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxScheme")?.Parent?
                    .Elements().FirstOrDefault(e => e.Name.LocalName == "CompanyID")?.Value.Trim()
                    ?? supplierParty.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxRegistrationNumber")?.Value.Trim();
            }

            var payable = Find("TaxInclusiveAmount") ?? Find("PayableAmount") ?? Find("LineExtensionAmount");
            if (payable is not null && decimal.TryParse(payable.Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var total))
                result.TotalAmount = total;

            var lineNodes = root.Descendants().Where(e =>
                e.Name.LocalName.Equals("InvoiceLine", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var ln in lineNodes)
            {
                var desc = ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value.Trim()
                           ?? ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "Name")?.Value.Trim()
                           ?? "";
                var qtyEl = ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "InvoicedQuantity");
                var qty = ParseDec(qtyEl?.Value) ?? 1m;
                var unit = qtyEl?.Attribute("unitCode")?.Value ?? "ks";
                var unitPrice = ParseDec(ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "UnitPrice")?.Value) ?? 0m;
                var lineTotal = ParseDec(ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "LineExtensionAmount")?.Value)
                                ?? unitPrice * qty;
                var vat = ParseDec(ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "Percent")?.Value) ?? 21m;
                var code = ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "ID")?.Value.Trim();
                var ean = ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "EAN")?.Value.Trim()
                          ?? ln.Descendants().FirstOrDefault(e => e.Name.LocalName == "CatalogueItemIdentification")?.Value.Trim();

                result.Lines.Add(new ParsedPurchaseInvoiceLine
                {
                    Description = desc,
                    Quantity = qty,
                    Unit = unit,
                    UnitPrice = unitPrice,
                    TaxRatePercent = vat,
                    LineTotal = lineTotal,
                    ProductCode = code,
                    Ean = ean
                });
            }

            if (result.TotalAmount <= 0)
                result.TotalAmount = result.Lines.Sum(l => l.LineTotal);

            return result.Lines.Count > 0 || !string.IsNullOrWhiteSpace(result.Number);
        }
        catch
        {
            return false;
        }
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)) return d;
        if (DateTime.TryParse(s, out d)) return d;
        return null;
    }

    private static decimal? ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
