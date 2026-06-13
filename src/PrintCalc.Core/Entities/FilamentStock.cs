namespace PrintCalc.Core.Entities;

/// <summary>Šarže / řádek skladu; zbývající hmotnost se snižuje výdeji.</summary>
public class FilamentStock
{
    public int Id { get; set; }
    public int FilamentTypeId { get; set; }
    public FilamentType FilamentType { get; set; } = null!;

    /// <summary>Číslo šarže / interní označení cívky.</summary>
    public string? LotNumber { get; set; }
    /// <summary>Datum expirace materiálu (volitelné).</summary>
    public DateTime? ExpirationDate { get; set; }
    public string? SupplierName { get; set; }
    public string? Notes { get; set; }
    public decimal PurchasePricePerKg { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public int PieceCount { get; set; } = 1;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public int? PurchaseInvoiceLineId { get; set; }
    public PurchaseInvoiceLine? PurchaseInvoiceLine { get; set; }
}
