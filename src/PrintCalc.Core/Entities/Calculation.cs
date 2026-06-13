namespace PrintCalc.Core.Entities;

public class Calculation
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? FilamentTypeId { get; set; }
    public FilamentType? FilamentType { get; set; }
    public int? PrinterId { get; set; }
    public Printer? Printer { get; set; }
    public int? PrintModelId { get; set; }
    public PrintModel? PrintModel { get; set; }

    public string? SourceModelPath { get; set; }
    public string? DrawingPdfPath { get; set; }

    public decimal MaterialGrams { get; set; }
    public decimal PrintHours { get; set; }
    public int PiecesPerBuild { get; set; } = 1;
    public int RequiredPieces { get; set; } = 1;
    public int PrintRuns { get; set; } = 1;
    public decimal MarginPercent { get; set; }
    public decimal ElectricityPricePerKwh { get; set; }
    /// <summary>Zákazník dodává vlastní materiál — cena materiálu 0 Kč, poznámka v nabídce a faktuře.</summary>
    public bool CustomerSuppliedMaterial { get; set; }

    public bool IncludeModelDesign { get; set; } = true;
    public decimal ModelDesignHours { get; set; }
    public decimal ModelDesignHourlyRate { get; set; }

    public decimal MaterialCost { get; set; }
    public decimal PrintCost { get; set; }
    public decimal EnergyCost { get; set; }
    public decimal ModelDesignCost { get; set; }
    /// <summary>Uplatněný pevný poplatek za tisk z profilu tiskárny, Kč.</summary>
    public decimal StartFeeCost { get; set; }

    /// <summary>Poplatek za přípravu dat ve sliceru (Kč), jednorázově na kalkulaci.</summary>
    public decimal SlicingFeePerModel { get; set; }
    public decimal SlicingFeeCost { get; set; }

    public decimal PostProcessingHours { get; set; }
    public decimal PostProcessingHourlyRate { get; set; }
    public decimal PostProcessingCost { get; set; }

    /// <summary>Koeficient zmetkovitosti aplikovaný na materiál (%).</summary>
    public decimal WasteCoefficientPercent { get; set; }

    public decimal QuantityDiscountPercent { get; set; }
    public decimal QuantityDiscountAmount { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountedSubtotal { get; set; }
    public decimal TotalWithMargin { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Orientační hmotnost na 1 kus podle PiecesPerBuild.</summary>
    public decimal MaterialGramsPerPiece =>
        Math.Round(MaterialGrams / Math.Max(1, PiecesPerBuild), 2, MidpointRounding.AwayFromZero);

    /// <summary>Orientační čas tisku na 1 kus podle PiecesPerBuild.</summary>
    public decimal PrintHoursPerPiece =>
        Math.Round(PrintHours / Math.Max(1, PiecesPerBuild), 3, MidpointRounding.AwayFromZero);

    public string Title { get; set; } = string.Empty;
    /// <summary>Volitelný popis položky „3D tisk“ při převodu do nabídky (místo výchozího textu z názvu kalkulace).</summary>
    public string? QuotePrintDescriptionOverride { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string ModeLabel =>
        CustomerSuppliedMaterial ? "MAT. ZÁKAZNÍK" :
        !IncludeModelDesign ? "REPRINT" :
        (PrintCost == 0 && MaterialCost == 0 && EnergyCost == 0 && ModelDesignCost > 0 ? "MODELOVANI ONLY" : "STANDARD");
}
