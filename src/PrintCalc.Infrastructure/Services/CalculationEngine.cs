using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class CalculationEngine : ICalculationEngine
{
    /// <summary>
    /// Materiál: g→kg × cena/kg × počet tisků (0 při materiálu zákazníka).
    /// Strojní čas: čas tisku × PrinterHourlyRate × počet tisků (hodinovka z karty tiskárny).
    /// Energie: čas × kWh/h × cena kWh × počet tisků.
    /// Start: poplatek/tisk z tiskárny × počet tisků. Marže ze součtu dílčích nákladů.
    /// </summary>
    public PriceQuote Compute(CalculationInput input)
    {
        var piecesPerBuild = Math.Max(1, input.PiecesPerBuild);
        var requiredPieces = Math.Max(1, input.RequiredPieces);
        var printRuns = (int)Math.Ceiling(requiredPieces / (decimal)piecesPerBuild);

        var materialKgPerBuild = input.MaterialGrams / 1000m;
        var materialCost = input.CustomerSuppliedMaterial
            ? 0m
            : materialKgPerBuild * input.FilamentPricePerKg * printRuns;
        var printCost = input.PrintHours * input.PrinterHourlyRate * printRuns;
        var energyCost = input.PrintHours * input.PrinterKwhPerHour * input.ElectricityPricePerKwh * printRuns;
        var modelDesignCost = input.ModelDesignHours * input.ModelDesignHourlyRate;
        var startFee = (input.StartFeePerPrint < 0 ? 0 : input.StartFeePerPrint) * printRuns;
        var subtotal = materialCost + printCost + energyCost + modelDesignCost + startFee;
        var marginMul = 1m + input.MarginPercent / 100m;
        var total = subtotal * marginMul;
        var unitForRequested = requiredPieces <= 0 ? total : total / requiredPieces;

        return new PriceQuote
        {
            PiecesPerBuild = piecesPerBuild,
            RequiredPieces = requiredPieces,
            PrintRuns = printRuns,
            MaterialCost = RoundMoney(materialCost),
            PrintCost = RoundMoney(printCost),
            EnergyCost = RoundMoney(energyCost),
            ModelDesignCost = RoundMoney(modelDesignCost),
            StartFeeCost = RoundMoney(startFee),
            Subtotal = RoundMoney(subtotal),
            TotalWithMargin = RoundMoney(total),
            UnitPriceForRequestedPiece = RoundMoney(unitForRequested)
        };
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
