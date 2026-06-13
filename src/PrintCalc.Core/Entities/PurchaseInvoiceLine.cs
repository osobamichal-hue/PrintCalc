using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class PurchaseInvoiceLine
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string Unit { get; set; } = "ks";
    public decimal UnitPrice { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal LineTotal { get; set; }
    public string? ProductCode { get; set; }
    public string? Ean { get; set; }

    public int? FilamentTypeId { get; set; }
    public FilamentType? FilamentType { get; set; }
    public PurchaseInvoiceLineMatchStatus MatchStatus { get; set; } = PurchaseInvoiceLineMatchStatus.Unmatched;
    public int MatchConfidence { get; set; }
    public decimal WeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public int PieceCount { get; set; } = 1;
}
