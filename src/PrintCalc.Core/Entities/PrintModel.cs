namespace PrintCalc.Core.Entities;

public class PrintModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Typ souboru (STL/3MF/GCODE).</summary>
    public string FileType { get; set; } = string.Empty;
    /// <summary>Původní cesta při importu (volitelné, pouze informativní).</summary>
    public string? FilePath { get; set; }
    /// <summary>Originální název souboru při importu.</summary>
    public string OriginalFileName { get; set; } = string.Empty;
    /// <summary>Binární obsah souboru uložený přímo v databázi.</summary>
    public byte[] FileContent { get; set; } = [];
    public decimal? EstimatedMaterialGrams { get; set; }
    public decimal? EstimatedPrintHours { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
