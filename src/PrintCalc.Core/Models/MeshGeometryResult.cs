namespace PrintCalc.Core.Models;

public class MeshGeometryResult
{
    public decimal VolumeCm3 { get; set; }
    public decimal SurfaceCm2 { get; set; }
    public decimal BboxXmm { get; set; }
    public decimal BboxYmm { get; set; }
    public decimal BboxZmm { get; set; }
    public int TriangleCount { get; set; }
    public List<string> Warnings { get; set; } = [];
}
