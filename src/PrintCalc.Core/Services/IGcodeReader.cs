using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IGcodeReader
{
    ThreeMfMetadata ReadMetadata(string filePath);
}
