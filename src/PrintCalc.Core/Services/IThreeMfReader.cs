using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IThreeMfReader
{
    ThreeMfMetadata ReadMetadata(string filePath);
}
