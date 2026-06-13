namespace PrintCalc.Core.Models;

public class CalculationInput
{
    public decimal MaterialGrams { get; set; }
    public decimal PrintHours { get; set; }
    public int PiecesPerBuild { get; set; } = 1;
    public int RequiredPieces { get; set; } = 1;
    public decimal FilamentPricePerKg { get; set; }
    public decimal PrinterHourlyRate { get; set; }
    public decimal PrinterKwhPerHour { get; set; }
    public decimal ModelDesignHours { get; set; }
    /// <summary>Globální hodinová sazba za tvorbu modelu.</summary>
    public decimal ModelDesignHourlyRate { get; set; }
    /// <summary>Pevný poplatek za jeden zahájený tisk (start fee), Kč.</summary>
    public decimal StartFeePerPrint { get; set; }
    public decimal ElectricityPricePerKwh { get; set; }
    public decimal MarginPercent { get; set; }

    /// <summary>Při true se cena materiálu nepočítá (materiál zákazníka).</summary>
    public bool CustomerSuppliedMaterial { get; set; }

    /// <summary>Jednorázový poplatek za přípravu dat ve sliceru (Kč), nezávislý na počtu tisků.</summary>
    public decimal SlicingFeePerModel { get; set; }

    /// <summary>Čas post-processingu (broušení, lakování…) v hodinách.</summary>
    public decimal PostProcessingHours { get; set; }

    /// <summary>Hodinová sazba post-processingu (Kč/h).</summary>
    public decimal PostProcessingHourlyRate { get; set; }

    /// <summary>Přírážka na materiál kvůli zmetkovitosti / supportům (%).</summary>
    public decimal WasteCoefficientPercent { get; set; }

    /// <summary>Množstevní slevová pásma (min kusů → sleva % ze subtotal před marží).</summary>
    public IReadOnlyList<QuantityDiscountTier>? QuantityDiscountTiers { get; set; }
}
