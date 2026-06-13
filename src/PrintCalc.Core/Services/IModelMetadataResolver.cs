using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IModelMetadataResolver
{
    ModelMetadataResult Resolve(string filePath, decimal densityGPerCm3 = 1.24m);
}
