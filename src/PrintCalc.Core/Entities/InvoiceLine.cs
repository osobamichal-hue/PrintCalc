namespace PrintCalc.Core.Entities;

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    /// <summary>Přeneseno ze zakázky (z nabídky) pro seskupení v PDF.</summary>
    public int? SourceCalculationId { get; set; }
    public int? SourceOrderId { get; set; }
    public int? SourceOrderLineId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal LineTotal { get; set; }
}
