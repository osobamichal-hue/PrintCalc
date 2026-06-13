using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Services;

public interface IQuotePdfService
{
    Task<string> SaveQuotePdfAsync(Quote quote, string outputDirectory, CancellationToken ct = default);
}
