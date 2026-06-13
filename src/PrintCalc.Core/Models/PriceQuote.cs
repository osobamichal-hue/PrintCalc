namespace PrintCalc.Core.Models;

public class PriceQuote
{
    public int PiecesPerBuild { get; set; }
    public int RequiredPieces { get; set; }
    public int PrintRuns { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal PrintCost { get; set; }
    public decimal EnergyCost { get; set; }
    public decimal ModelDesignCost { get; set; }
    public decimal StartFeeCost { get; set; }
    public decimal SlicingFeeCost { get; set; }
    public decimal PostProcessingCost { get; set; }
    public decimal WasteCoefficientPercent { get; set; }
    public decimal QuantityDiscountPercent { get; set; }
    public decimal QuantityDiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountedSubtotal { get; set; }
    public decimal TotalWithMargin { get; set; }
    public decimal UnitPriceForRequestedPiece { get; set; }
}
