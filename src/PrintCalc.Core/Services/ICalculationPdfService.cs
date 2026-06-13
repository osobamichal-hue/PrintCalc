using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Services;

public interface ICalculationPdfService
{
    Task<string> SaveCalculationPdfAsync(Calculation calculation, string outputDirectory, CancellationToken ct = default);
}
