using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Models;

public class ModelMetadataResult
{
    public decimal? MaterialGrams { get; set; }
    public decimal? PrintHours { get; set; }
    public decimal? VolumeCm3 { get; set; }
    public decimal? SurfaceCm2 { get; set; }
    public decimal? BboxXmm { get; set; }
    public decimal? BboxYmm { get; set; }
    public decimal? BboxZmm { get; set; }
    public EstimateSource EstimateSource { get; set; } = EstimateSource.Unknown;
    public List<string> Warnings { get; set; } = [];
}
