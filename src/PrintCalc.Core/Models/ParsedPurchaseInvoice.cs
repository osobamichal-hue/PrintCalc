namespace PrintCalc.Core.Models;

public class ParsedPurchaseInvoiceLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string Unit { get; set; } = "ks";
    public decimal UnitPrice { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal LineTotal { get; set; }
    public string? ProductCode { get; set; }
    public string? Ean { get; set; }
}

public class ParsedPurchaseInvoice
{
    public string Number { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCompanyId { get; set; }
    public string? SupplierVatId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<ParsedPurchaseInvoiceLine> Lines { get; set; } = new();
}
