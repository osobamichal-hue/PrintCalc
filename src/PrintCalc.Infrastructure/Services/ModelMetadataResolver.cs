using PrintCalc.Core.Enums;
using PrintCalc.Core.Models;
using PrintCalc.Core.Services;

namespace PrintCalc.Infrastructure.Services;

public class ModelMetadataResolver : IModelMetadataResolver
{
    private readonly IThreeMfReader _threeMf;
    private readonly IGcodeReader _gcode;
    private readonly IMeshReader _mesh;

    public ModelMetadataResolver(IThreeMfReader threeMf, IGcodeReader gcode, IMeshReader mesh)
    {
        _threeMf = threeMf;
        _gcode = gcode;
        _mesh = mesh;
    }

    public ModelMetadataResult Resolve(string filePath, decimal densityGPerCm3 = 1.24m)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var result = new ModelMetadataResult();

        if (ext == ".3mf")
        {
            var meta = _threeMf.ReadMetadata(filePath);
            result.MaterialGrams = meta.MaterialGrams;
            result.PrintHours = meta.PrintHours;
            AddWarnings(result, meta.Warnings);
            if (meta.MaterialGrams is not null || meta.PrintHours is not null)
                result.EstimateSource = EstimateSource.SlicerMetadata;
        }
        else if (ext is ".gcode" or ".gco")
        {
            var meta = _gcode.ReadMetadata(filePath);
            result.MaterialGrams = meta.MaterialGrams;
            result.PrintHours = meta.PrintHours;
            AddWarnings(result, meta.Warnings);
            if (meta.MaterialGrams is not null || meta.PrintHours is not null)
                result.EstimateSource = EstimateSource.SlicerMetadata;
        }

        if (ext is ".stl" or ".obj" or ".3mf")
            TryApplyMeshGeometry(filePath, result, densityGPerCm3);

        if (result.MaterialGrams is null && result.PrintHours is null)
            result.Warnings.Add("V souboru nebyly spolehlivě nalezeny hmotnost ani čas – doplňte ručně.");

        if (result.MaterialGrams is not null && result.PrintHours is null)
            result.Warnings.Add("Čas tisku nebyl odhadnut – doplňte ručně nebo použijte export ze sliceru.");

        return result;
    }

    private static void AddWarnings(ModelMetadataResult result, IEnumerable<string> warnings)
    {
        foreach (var w in warnings)
        {
            if (string.IsNullOrWhiteSpace(w)) continue;
            if (result.Warnings.Contains(w)) continue;
            result.Warnings.Add(w);
        }
    }

    private void TryApplyMeshGeometry(string filePath, ModelMetadataResult result, decimal densityGPerCm3)
    {
        var geo = _mesh.ReadGeometry(filePath);
        if (geo.TriangleCount == 0 && geo.VolumeCm3 <= 0) return;

        result.VolumeCm3 = geo.VolumeCm3;
        result.SurfaceCm2 = geo.SurfaceCm2;
        result.BboxXmm = geo.BboxXmm;
        result.BboxYmm = geo.BboxYmm;
        result.BboxZmm = geo.BboxZmm;
        AddWarnings(result, geo.Warnings);

        if (result.MaterialGrams is null && geo.VolumeCm3 > 0 && densityGPerCm3 > 0)
        {
            result.MaterialGrams = Math.Round(geo.VolumeCm3 * densityGPerCm3, 2, MidpointRounding.AwayFromZero);
            if (result.EstimateSource == EstimateSource.Unknown)
                result.EstimateSource = EstimateSource.GeometryEstimate;
            AddWarnings(result, [$"Hmotnost odhadnuta z objemu ({geo.VolumeCm3:N2} cm³ × {densityGPerCm3:N2} g/cm³) – bez supportů a infillu."]);
        }
    }
}
