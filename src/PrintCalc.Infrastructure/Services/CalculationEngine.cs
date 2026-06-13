using PrintCalc.Core.Helpers;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class CalculationEngine : ICalculationEngine
{
    /// <summary>
    /// Materiál: g→kg × cena/kg × počet tisků × (1 + waste%) (0 při materiálu zákazníka).
    /// Strojní čas: čas tisku × PrinterHourlyRate × počet tisků.
    /// Energie: čas × kWh/h × cena kWh × počet tisků.
    /// Start: poplatek/tisk z tiskárny × počet tisků.
    /// Slicing fee: jednorázově na kalkulaci. Post-processing: hodiny × sazba.
    /// Množstevní sleva ze subtotal před marží. Marže z discounted subtotal.
    /// </summary>
    public PriceQuote Compute(CalculationInput input)
    {
        var piecesPerBuild = Math.Max(1, input.PiecesPerBuild);
        var requiredPieces = Math.Max(1, input.RequiredPieces);
        var printRuns = (int)Math.Ceiling(requiredPieces / (decimal)piecesPerBuild);

        var materialKgPerBuild = input.MaterialGrams / 1000m;
        var materialBase = input.CustomerSuppliedMaterial
            ? 0m
            : materialKgPerBuild * input.FilamentPricePerKg * printRuns;
        var wasteMul = 1m + Math.Max(0, input.WasteCoefficientPercent) / 100m;
        var materialCost = materialBase * wasteMul;

        var printCost = input.PrintHours * input.PrinterHourlyRate * printRuns;
        var energyCost = input.PrintHours * input.PrinterKwhPerHour * input.ElectricityPricePerKwh * printRuns;
        var modelDesignCost = input.ModelDesignHours * input.ModelDesignHourlyRate;
        var startFee = (input.StartFeePerPrint < 0 ? 0 : input.StartFeePerPrint) * printRuns;
        var slicingFee = Math.Max(0, input.SlicingFeePerModel);
        var postProcessingCost = Math.Max(0, input.PostProcessingHours) * Math.Max(0, input.PostProcessingHourlyRate);

        var subtotal = materialCost + printCost + energyCost + modelDesignCost + startFee + slicingFee + postProcessingCost;

        var tiers = input.QuantityDiscountTiers is { Count: > 0 } t
            ? t
            : QuantityDiscountHelper.DefaultTiers;
        var discountPercent = QuantityDiscountHelper.ResolveDiscountPercent(requiredPieces, tiers);
        var discountAmount = subtotal * (discountPercent / 100m);
        var discountedSubtotal = subtotal - discountAmount;

        var total = discountedSubtotal * (1m + input.MarginPercent / 100m);
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
            SlicingFeeCost = RoundMoney(slicingFee),
            PostProcessingCost = RoundMoney(postProcessingCost),
            WasteCoefficientPercent = Math.Max(0, input.WasteCoefficientPercent),
            QuantityDiscountPercent = discountPercent,
            QuantityDiscountAmount = RoundMoney(discountAmount),
            Subtotal = RoundMoney(subtotal),
            DiscountedSubtotal = RoundMoney(discountedSubtotal),
            TotalWithMargin = RoundMoney(total),
            UnitPriceForRequestedPiece = RoundMoney(unitForRequested)
        };
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
