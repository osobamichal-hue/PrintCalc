namespace PrintCalc.Core.Entities;

public class FilamentType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public decimal DiameterMm { get; set; } = 1.75m;
    public string? Color { get; set; }
    public decimal DensityGPerCm3 { get; set; } = 1.24m;
    public int? NozzleTempMinC { get; set; }
    public int? NozzleTempMaxC { get; set; }
    public int? BedTempMinC { get; set; }
    public int? BedTempMaxC { get; set; }
    /// <summary>Vážený průměr z aktuálních zásob (Kč/kg).</summary>
    public decimal AveragePricePerKg { get; set; }
    /// <summary>Minimální zásoba (kg) — pod touto hranicí se zobrazí upozornění.</summary>
    public decimal MinStockKg { get; set; }
    public string? Notes { get; set; }

    public ICollection<FilamentStock> Stocks { get; set; } = new List<FilamentStock>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<PrinterFilamentType> PrinterLinks { get; set; } = new List<PrinterFilamentType>();
}
