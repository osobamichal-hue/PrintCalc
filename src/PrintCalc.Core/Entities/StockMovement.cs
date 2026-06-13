using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class StockMovement
{
    public int Id { get; set; }
    public int FilamentTypeId { get; set; }
    public FilamentType FilamentType { get; set; } = null!;

    public StockMovementType MovementType { get; set; }
    public decimal DeltaKg { get; set; }
    public decimal? UnitPricePerKg { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public int? FilamentStockId { get; set; }
    public FilamentStock? FilamentStock { get; set; }
    public int? PurchaseInvoiceLineId { get; set; }
    public PurchaseInvoiceLine? PurchaseInvoiceLine { get; set; }
    public int? CalculationId { get; set; }
    public Calculation? Calculation { get; set; }
}
