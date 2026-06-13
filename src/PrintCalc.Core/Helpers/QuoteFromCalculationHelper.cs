using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Helpers;

/// <summary>
/// Jednotná logika řádků nabídky z kalkulace: součet řádků = TotalWithMargin, bez matoucí „korekce“ při modelování + tisku.
/// </summary>
public static class QuoteFromCalculationHelper
{
    /// <summary>Text do nabídky a dokumentů při tisku z materiálu zákazníka.</summary>
    public const string CustomerSuppliedMaterialNote = "tisk z materiálu zákazníka";

    private static string CustomerMaterialSuffix(bool customerSuppliedMaterial) =>
        customerSuppliedMaterial ? $" — {CustomerSuppliedMaterialNote}" : "";

    /// <summary>Popis řádku za 3D tisk — vlastní text nebo výchozí „název - 3D tisk“ + případná poznámka k materiálu.</summary>
    public static string BuildPrintLineDescription(string? quotePrintOverride, bool customerSuppliedMaterial, string label)
    {
        var core = string.IsNullOrWhiteSpace(quotePrintOverride)
            ? $"{label} - 3D tisk"
            : quotePrintOverride.Trim();
        return core + CustomerMaterialSuffix(customerSuppliedMaterial);
    }

    /// <summary>Stejné jako přetížení se stringy, čte se z entity kalkulace.</summary>
    public static string BuildPrintLineDescription(Calculation calc, string label) =>
        BuildPrintLineDescription(calc.QuotePrintDescriptionOverride, calc.CustomerSuppliedMaterial, label);

    public static void AddDetailedLines(Quote quote, Calculation calc)
    {
        var requiredPieces = calc.RequiredPieces <= 0 ? 1 : calc.RequiredPieces;
        var printRuns = calc.PrintRuns <= 0 ? 1 : calc.PrintRuns;
        var label = string.IsNullOrWhiteSpace(calc.Title) ? $"Kalkulace #{calc.Id}" : calc.Title.Trim();

        var hasModeling = calc.ModelDesignCost > 0;
        if (hasModeling)
        {
            var modelHours = calc.ModelDesignHours <= 0 ? 0 : calc.ModelDesignHours;
            var modelRate = modelHours > 0
                ? Math.Round(calc.ModelDesignCost / modelHours, 0, MidpointRounding.AwayFromZero)
                : calc.ModelDesignHourlyRate;
            quote.Lines.Add(new QuoteLine
            {
                SourceCalculationId = calc.Id,
                Description = $"{label} - Modelování ({modelHours:0.##} h × {modelRate:0} Kč/h)",
                Quantity = 1,
                UnitPrice = calc.ModelDesignCost,
                LineTotal = calc.ModelDesignCost
            });
        }

        var printPartTotal = calc.TotalWithMargin - (hasModeling ? calc.ModelDesignCost : 0m);
        if (printPartTotal < 0) printPartTotal = 0;

        if (printPartTotal > 0)
        {
            if (hasModeling)
            {
                quote.Lines.Add(new QuoteLine
                {
                    SourceCalculationId = calc.Id,
                    Description = BuildPrintLineDescription(calc, label),
                    Quantity = 1,
                    UnitPrice = printPartTotal,
                    LineTotal = printPartTotal
                });
            }
            else
            {
                var unit = requiredPieces > 0
                    ? printPartTotal / requiredPieces
                    : printPartTotal;
                var lineTotal = Math.Round(unit * requiredPieces, 0, MidpointRounding.AwayFromZero);
                quote.Lines.Add(new QuoteLine
                {
                    SourceCalculationId = calc.Id,
                    Description = BuildPrintLineDescription(calc, label),
                    Quantity = requiredPieces,
                    UnitPrice = unit,
                    LineTotal = lineTotal
                });
                var delta = Math.Round(calc.TotalWithMargin - quote.Lines.Sum(x => x.LineTotal), 0, MidpointRounding.AwayFromZero);
                if (Math.Abs(delta) > 0 && Math.Abs(delta) <= 1m)
                {
                    quote.Lines.Add(new QuoteLine
                    {
                        SourceCalculationId = calc.Id,
                        Description = $"{label} - Dopočet (zaokrouhlení)",
                        Quantity = 1,
                        UnitPrice = delta,
                        LineTotal = delta
                    });
                }
            }
        }
    }
}
