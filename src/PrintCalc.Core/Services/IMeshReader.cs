using PrintCalc.Core.Models;

namespace PrintCalc.Core.Services;

public interface IMeshReader
{
    MeshGeometryResult ReadGeometry(string filePath);
}
