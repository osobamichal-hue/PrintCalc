namespace PrintCalc.Core.Entities;

public class QuoteLine
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    /// <summary>ID zdrojové kalkulace při převodu do nabídky; bez FK v DB — zůstane i po smazání kalkulace.</summary>
    public int? SourceCalculationId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
