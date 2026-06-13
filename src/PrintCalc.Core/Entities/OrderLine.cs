namespace PrintCalc.Core.Entities;

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>Přeneseno z nabídky pro seskupení v PDF / přehledech.</summary>
    public int? SourceCalculationId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
