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
    public decimal Subtotal { get; set; }
    public decimal TotalWithMargin { get; set; }
    public decimal UnitPriceForRequestedPiece { get; set; }
}
