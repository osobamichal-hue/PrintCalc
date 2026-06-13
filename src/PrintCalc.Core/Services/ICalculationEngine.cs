using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface ICalculationEngine
{
    PriceQuote Compute(CalculationInput input);
}
