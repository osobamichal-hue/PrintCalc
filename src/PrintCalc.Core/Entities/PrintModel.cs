using PrintCalc.Core.Enums;

namespace PrintCalc.Core.Entities;

public class PrintModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Typ souboru (STL/OBJ/3MF/GCODE).</summary>
    public string FileType { get; set; } = string.Empty;
    /// <summary>Původní cesta při importu (volitelné, pouze informativní).</summary>
    public string? FilePath { get; set; }
    /// <summary>Originální název souboru při importu.</summary>
    public string OriginalFileName { get; set; } = string.Empty;
    /// <summary>Binární obsah souboru uložený přímo v databázi.</summary>
    public byte[] FileContent { get; set; } = [];
    public decimal? EstimatedMaterialGrams { get; set; }
    public decimal? EstimatedPrintHours { get; set; }

    public decimal? VolumeCm3 { get; set; }
    public decimal? SurfaceCm2 { get; set; }
    public decimal? BboxXmm { get; set; }
    public decimal? BboxYmm { get; set; }
    public decimal? BboxZmm { get; set; }
    public EstimateSource EstimateSource { get; set; } = EstimateSource.Unknown;
    public string? GeometryWarnings { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
